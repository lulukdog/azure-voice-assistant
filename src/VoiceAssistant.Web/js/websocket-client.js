/**
 * SignalR WebSocket client for Azure Voice Assistant.
 *
 * Depends on the Microsoft SignalR JavaScript client library being loaded
 * globally as `signalR` (typically via CDN).
 *
 * Usage:
 *   const client = new WebSocketClient();
 *   client.on('connected', () => { ... });
 *   client.on('recognitionResult', (data) => { ... });
 *   await client.connect();
 *   await client.startSession('zh-CN');
 *   await client.sendAudio(base64String);
 *   await client.endSession();
 */
(function () {
    'use strict';

    class WebSocketClient {
        constructor() {
            /** @type {signalR.HubConnection|null} */
            this.connection = null;

            /** @type {string|null} */
            this.sessionId = null;

            /** @type {boolean} */
            this.isConnected = false;

            /**
             * Registered event callbacks.
             * @type {Object.<string, Function>}
             * @private
             */
            this._callbacks = {};
        }

        // ----------------------------------------------------------------
        // Public API - event registration
        // ----------------------------------------------------------------

        /**
         * Register a callback for a named event.
         * @param {string} event  Event name.
         * @param {Function} callback  Handler function.
         */
        on(event, callback) {
            this._callbacks[event] = callback;
        }

        /**
         * Remove a previously registered callback.
         * @param {string} event  Event name.
         */
        off(event) {
            delete this._callbacks[event];
        }

        // ----------------------------------------------------------------
        // Public API - connection lifecycle
        // ----------------------------------------------------------------

        /**
         * Build the SignalR connection, register all server-to-client handlers,
         * and start the connection.
         * @returns {Promise<void>}
         */
        async connect() {
            if (this.connection) {
                console.log('[WebSocketClient] Connection already exists, disconnecting first.');
                await this.disconnect();
            }

            console.log('[WebSocketClient] Building SignalR connection to /hubs/voice');

            this.connection = new signalR.HubConnectionBuilder()
                .withUrl('/hubs/voice')
                .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
                .build();

            // --- Register server-to-client event handlers ----------------

            this._registerServerEvents();

            // --- Connection lifecycle events ------------------------------

            this.connection.onreconnecting(function (error) {
                console.log('[WebSocketClient] Reconnecting...', error ? error.message : '');
                this.isConnected = false;
                this._emit('reconnecting', { error: error });
            }.bind(this));

            this.connection.onreconnected(function (connectionId) {
                console.log('[WebSocketClient] Reconnected. Connection ID:', connectionId);
                this.isConnected = true;
                this._emit('reconnected', { connectionId: connectionId });
            }.bind(this));

            this.connection.onclose(function (error) {
                console.log('[WebSocketClient] Connection closed.', error ? error.message : '');
                this.isConnected = false;
                this.sessionId = null;
                this._emit('disconnected', { error: error });
            }.bind(this));

            // --- Start ---------------------------------------------------

            try {
                await this.connection.start();
                this.isConnected = true;
                console.log('[WebSocketClient] Connected successfully.');
                this._emit('connected', {});
            } catch (err) {
                console.error('[WebSocketClient] Failed to connect:', err);
                this.isConnected = false;
                this._emit('error', { code: 'CONNECTION_FAILED', message: err.message });
                throw err;
            }
        }

        /**
         * Stop the SignalR connection gracefully.
         * @returns {Promise<void>}
         */
        async disconnect() {
            if (!this.connection) {
                return;
            }

            try {
                await this.connection.stop();
                console.log('[WebSocketClient] Disconnected.');
            } catch (err) {
                console.error('[WebSocketClient] Error during disconnect:', err);
            } finally {
                this.isConnected = false;
                this.sessionId = null;
                this.connection = null;
            }
        }

        // ----------------------------------------------------------------
        // Public API - client-to-server methods
        // ----------------------------------------------------------------

        /**
         * Ask the server to start a new voice session.
         * @param {string} [language='zh-CN']  BCP-47 language tag.
         * @returns {Promise<void>}
         */
        async startSession(language) {
            language = language || 'zh-CN';

            this._ensureConnected();

            console.log('[WebSocketClient] StartSession, language:', language);

            try {
                await this.connection.invoke('StartSession', language);
            } catch (err) {
                console.error('[WebSocketClient] StartSession failed:', err);
                this._emit('error', { code: 'START_SESSION_FAILED', message: err.message });
                throw err;
            }
        }

        /**
         * Send a complete audio chunk (base64-encoded) to the server.
         * Uses the stored sessionId from the most recent SessionStarted event.
         * @param {string} audioBase64  Base64-encoded audio data.
         * @returns {Promise<void>}
         */
        async sendAudio(audioBase64) {
            this._ensureConnected();

            if (!this.sessionId) {
                var msg = 'No active session. Call startSession() first.';
                console.error('[WebSocketClient]', msg);
                throw new Error(msg);
            }

            console.log('[WebSocketClient] SendAudio, sessionId:', this.sessionId,
                ', size:', audioBase64.length, 'chars');

            try {
                await this.connection.invoke('SendAudio', this.sessionId, audioBase64);
            } catch (err) {
                console.error('[WebSocketClient] SendAudio failed:', err);
                this._emit('error', { code: 'SEND_AUDIO_FAILED', message: err.message });
                throw err;
            }
        }

        /**
         * End the current session.
         * @returns {Promise<void>}
         */
        async endSession() {
            this._ensureConnected();

            if (!this.sessionId) {
                console.log('[WebSocketClient] No active session to end.');
                return;
            }

            var sessionId = this.sessionId;
            console.log('[WebSocketClient] EndSession, sessionId:', sessionId);

            try {
                await this.connection.invoke('EndSession', sessionId);
            } catch (err) {
                console.error('[WebSocketClient] EndSession failed:', err);
                this._emit('error', { code: 'END_SESSION_FAILED', message: err.message });
                throw err;
            }
        }

        // ----------------------------------------------------------------
        // Private helpers
        // ----------------------------------------------------------------

        /**
         * Fire a registered callback for the given event name.
         * @param {string} event  Event name.
         * @param {*} data  Data payload passed to the callback.
         * @private
         */
        _emit(event, data) {
            var callback = this._callbacks[event];
            if (callback) {
                try {
                    callback(data);
                } catch (err) {
                    console.error('[WebSocketClient] Error in "' + event + '" callback:', err);
                }
            }
        }

        /**
         * Guard that throws if the connection is not established.
         * @private
         */
        _ensureConnected() {
            if (!this.connection || !this.isConnected) {
                var msg = 'Not connected. Call connect() first.';
                console.error('[WebSocketClient]', msg);
                throw new Error(msg);
            }
        }

        /**
         * Register all server-to-client SignalR event handlers on the current
         * connection instance.
         * @private
         */
        _registerServerEvents() {
            var self = this;

            // SessionStarted ------------------------------------------------
            this.connection.on('SessionStarted', function (data) {
                console.log('[WebSocketClient] SessionStarted, sessionId:', data.sessionId);
                self.sessionId = data.sessionId;
                self._emit('sessionStarted', data);
            });

            // RecognitionResult ----------------------------------------------
            this.connection.on('RecognitionResult', function (data) {
                console.log('[WebSocketClient] RecognitionResult, text:', data.text,
                    ', confidence:', data.confidence,
                    ', isFinal:', data.isFinal);
                self._emit('recognitionResult', data);
            });

            // AssistantTextChunk ---------------------------------------------
            this.connection.on('AssistantTextChunk', function (data) {
                console.log('[WebSocketClient] AssistantTextChunk, isComplete:', data.isComplete,
                    ', chunk:', data.textChunk);
                self._emit('assistantTextChunk', data);
            });

            // AudioChunk -----------------------------------------------------
            this.connection.on('AudioChunk', function (data) {
                console.log('[WebSocketClient] AudioChunk, contentType:', data.contentType,
                    ', isComplete:', data.isComplete);
                self._emit('audioChunk', data);
            });

            // SessionEnded ---------------------------------------------------
            this.connection.on('SessionEnded', function (data) {
                console.log('[WebSocketClient] SessionEnded, sessionId:', data.sessionId);
                self.sessionId = null;
                self._emit('sessionEnded', data);
            });

            // Error ----------------------------------------------------------
            this.connection.on('Error', function (data) {
                console.error('[WebSocketClient] Server error, code:', data.code,
                    ', message:', data.message);
                self._emit('error', data);
            });
        }
    }

    // Expose globally
    window.WebSocketClient = WebSocketClient;

})();
