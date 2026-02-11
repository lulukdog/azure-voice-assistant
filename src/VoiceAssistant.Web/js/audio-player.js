/**
 * AudioPlayer - Browser audio playback module for TTS responses.
 *
 * Plays base64-encoded audio chunks (MP3 or WAV) received from the server.
 * Supports streaming (queued chunks) and single-shot playback, with
 * immediate interruption so the user can start a new recording at any time.
 *
 * Events emitted via on()/off():
 *   'started'  - playback has begun
 *   'ended'    - playback finished naturally (queue drained)
 *   'stopped'  - playback was interrupted by stop()
 *   'error'    - a decode or playback error occurred (data: Error)
 */
class AudioPlayer {
    constructor() {
        /** @type {AudioContext|null} */
        this.audioContext = null;

        /** Whether audio is currently being output. */
        this.isPlaying = false;

        /** The AudioBufferSourceNode that is currently playing. */
        this.currentSource = null;

        /** Registered event callbacks keyed by event name. */
        this._callbacks = {};

        /** Queue of decoded AudioBuffer objects waiting to be played. */
        this._audioQueue = [];

        /** True while we are actively decoding / playing through the queue. */
        this._isProcessing = false;

        /** Set to true once the final chunk has been enqueued. */
        this._streamComplete = false;
    }

    // ------------------------------------------------------------------ events

    /**
     * Register a callback for an event.
     * @param {string} event  One of 'started', 'ended', 'stopped', 'error'.
     * @param {Function} callback
     */
    on(event, callback) {
        if (!this._callbacks[event]) {
            this._callbacks[event] = [];
        }
        this._callbacks[event].push(callback);
    }

    /**
     * Remove a previously registered callback.
     * @param {string} event
     * @param {Function} callback
     */
    off(event, callback) {
        if (!this._callbacks[event]) return;
        this._callbacks[event] = this._callbacks[event].filter(function (cb) {
            return cb !== callback;
        });
    }

    /**
     * Emit an event to all registered listeners.
     * @param {string} event
     * @param {*} [data]
     */
    _emit(event, data) {
        var listeners = this._callbacks[event];
        if (!listeners) return;
        for (var i = 0; i < listeners.length; i++) {
            try {
                listeners[i](data);
            } catch (err) {
                console.error('[AudioPlayer] Error in "' + event + '" listener:', err);
            }
        }
    }

    // ---------------------------------------------------------- initialisation

    /**
     * Initialise (or resume) the AudioContext.
     *
     * Must be called from inside a user-gesture handler the first time,
     * because browsers require a user interaction before audio can play.
     */
    init() {
        if (!this.audioContext) {
            var AudioContextClass = window.AudioContext || window.webkitAudioContext;
            if (!AudioContextClass) {
                console.error('[AudioPlayer] Web Audio API is not supported in this browser.');
                return;
            }
            this.audioContext = new AudioContextClass();
        }

        // If the context was suspended (e.g. backgrounded tab), resume it.
        if (this.audioContext.state === 'suspended') {
            this.audioContext.resume();
        }
    }

    // --------------------------------------------------- streaming (chunked) API

    /**
     * Add a base64-encoded audio chunk to the play queue.
     *
     * The chunk is decoded asynchronously; once ready it is appended to an
     * internal queue.  If nothing is currently playing, playback starts
     * automatically.
     *
     * @param {string}  audioBase64   Base64-encoded audio data (MP3 or WAV).
     * @param {string}  contentType   MIME type, e.g. "audio/mp3" or "audio/wav".
     * @param {boolean} isComplete    If true, this is the last chunk in the stream.
     */
    async addChunk(audioBase64, contentType, isComplete) {
        this.init();

        if (isComplete) {
            this._streamComplete = true;
        }

        try {
            var arrayBuffer = this._base64ToArrayBuffer(audioBase64);
            var audioBuffer = await this._decodeAudioData(arrayBuffer);

            if (!audioBuffer) {
                // Decoding failed (warning already logged inside helper).
                // If this was the last chunk and nothing is playing, signal end.
                if (this._streamComplete && this._audioQueue.length === 0 && !this._isProcessing) {
                    this.isPlaying = false;
                    this._emit('ended');
                }
                return;
            }

            this._audioQueue.push(audioBuffer);

            // Kick off playback if we are not already processing the queue.
            if (!this._isProcessing) {
                this._isProcessing = true;
                this._emit('started');
                this._playNext();
            }
        } catch (err) {
            console.warn('[AudioPlayer] Failed to process audio chunk:', err);
            this._emit('error', err);
        }
    }

    // ------------------------------------------------ single-shot playback API

    /**
     * Play a complete base64-encoded audio clip (non-streaming).
     *
     * Any currently playing audio is stopped first.
     *
     * @param {string} audioBase64  Base64-encoded audio data.
     * @param {string} contentType  MIME type, e.g. "audio/mp3" or "audio/wav".
     */
    async play(audioBase64, contentType) {
        this.init();

        // Stop whatever is currently playing / queued.
        this.stop();

        try {
            var arrayBuffer = this._base64ToArrayBuffer(audioBase64);
            var audioBuffer = await this._decodeAudioData(arrayBuffer);

            if (!audioBuffer) {
                return;
            }

            var source = this.audioContext.createBufferSource();
            source.buffer = audioBuffer;
            source.connect(this.audioContext.destination);

            this.currentSource = source;
            this.isPlaying = true;
            this._emit('started');

            var self = this;
            source.onended = function () {
                // Only emit 'ended' if we were not already stopped externally.
                if (self.currentSource === source) {
                    self.currentSource = null;
                    self.isPlaying = false;
                    self._emit('ended');
                }
            };

            source.start(0);
        } catch (err) {
            console.error('[AudioPlayer] Playback error:', err);
            this.isPlaying = false;
            this._emit('error', err);
        }
    }

    // ----------------------------------------------------------- stop / interrupt

    /**
     * Immediately stop all playback and clear the queue.
     *
     * This is the method to call when the user starts a new recording and
     * the current TTS response must be silenced right away.
     */
    stop() {
        var wasPlaying = this.isPlaying;

        // Stop the currently playing source node.
        if (this.currentSource) {
            try {
                this.currentSource.onended = null; // prevent the 'ended' cascade
                this.currentSource.stop();
            } catch (_ignored) {
                // source.stop() throws if the node has already been stopped.
            }
            this.currentSource = null;
        }

        // Drain the queue.
        this._audioQueue.length = 0;

        // Reset state.
        this.isPlaying = false;
        this._isProcessing = false;
        this._streamComplete = false;

        if (wasPlaying) {
            this._emit('stopped');
        }
    }

    // ---------------------------------------------------- internal queue player

    /**
     * Play the next AudioBuffer in the queue.
     *
     * When the buffer finishes, this method calls itself recursively to
     * continue through the queue until it is empty.
     */
    async _playNext() {
        if (this._audioQueue.length === 0) {
            // If the stream is complete (no more chunks coming), we are done.
            // Otherwise, keep _isProcessing true and wait for the next addChunk.
            if (this._streamComplete) {
                this._isProcessing = false;
                this.isPlaying = false;
                this._emit('ended');
            } else {
                // Temporarily idle; addChunk will re-trigger _playNext.
                this._isProcessing = false;
            }
            return;
        }

        var buffer = this._audioQueue.shift();
        var source = this.audioContext.createBufferSource();
        source.buffer = buffer;
        source.connect(this.audioContext.destination);

        this.currentSource = source;
        this.isPlaying = true;

        var self = this;
        source.onended = function () {
            if (self.currentSource === source) {
                self.currentSource = null;
                self._playNext();
            }
        };

        source.start(0);
    }

    // --------------------------------------------------------- helper utilities

    /**
     * Decode an ArrayBuffer into an AudioBuffer.
     *
     * Returns null if decoding fails (a warning is logged).
     *
     * @param {ArrayBuffer} arrayBuffer
     * @returns {Promise<AudioBuffer|null>}
     */
    async _decodeAudioData(arrayBuffer) {
        try {
            var audioBuffer = await this.audioContext.decodeAudioData(arrayBuffer);
            return audioBuffer;
        } catch (err) {
            console.warn('[AudioPlayer] Failed to decode audio data, skipping chunk:', err);
            return null;
        }
    }

    /**
     * Convert a base64 string to an ArrayBuffer.
     *
     * @param {string} base64
     * @returns {ArrayBuffer}
     */
    _base64ToArrayBuffer(base64) {
        var binaryString = atob(base64);
        var length = binaryString.length;
        var bytes = new Uint8Array(length);
        for (var i = 0; i < length; i++) {
            bytes[i] = binaryString.charCodeAt(i);
        }
        return bytes.buffer;
    }

    // ----------------------------------------------------------------- cleanup

    /**
     * Stop playback and release the AudioContext.
     *
     * After calling dispose(), this instance should not be reused.
     */
    dispose() {
        this.stop();

        if (this.audioContext) {
            try {
                this.audioContext.close();
            } catch (_ignored) {
                // close() can throw if the context is already closed.
            }
            this.audioContext = null;
        }

        this._callbacks = {};
    }
}

// Export as a global so other plain-JS modules can use it.
window.AudioPlayer = AudioPlayer;
