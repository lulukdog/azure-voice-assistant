/**
 * AudioRecorder - Browser audio recording module using Web Audio API.
 * Captures microphone audio in PCM 16kHz 16-bit mono format (WAV).
 * Designed for Azure Speech Service integration via WebSocket.
 */
class AudioRecorder {
    constructor() {
        this.mediaStream = null;
        this.audioContext = null;
        this.sourceNode = null;
        this.processor = null;
        this.isRecording = false;
        this.audioChunks = [];
        this.startTime = null;
        this.maxDuration = 60;
        this._callbacks = {};
        this._volumeTimer = null;
        this._currentVolume = 0;
    }

    /**
     * Register an event callback.
     * Supported events: 'started', 'stopped', 'volume', 'maxDurationReached', 'error'
     * @param {string} event
     * @param {Function} callback
     */
    on(event, callback) {
        this._callbacks[event] = callback;
    }

    /**
     * Emit an event to the registered callback.
     * @param {string} event
     * @param {*} data
     */
    _emit(event, data) {
        if (this._callbacks[event]) {
            this._callbacks[event](data);
        }
    }

    /**
     * Start recording from the microphone.
     * Requests mic permission, creates an AudioContext at 16kHz,
     * and begins capturing PCM samples via ScriptProcessorNode.
     */
    async start() {
        if (this.isRecording) {
            return;
        }

        try {
            this.mediaStream = await navigator.mediaDevices.getUserMedia({
                audio: {
                    channelCount: 1,
                    sampleRate: 16000,
                    echoCancellation: true,
                    noiseSuppression: true
                }
            });
        } catch (err) {
            this._emit('error', {
                type: 'permission_denied',
                message: 'Microphone access denied: ' + err.message
            });
            return;
        }

        try {
            this.audioContext = new (window.AudioContext || window.webkitAudioContext)({
                sampleRate: 16000
            });

            this.sourceNode = this.audioContext.createMediaStreamSource(this.mediaStream);

            // ScriptProcessorNode: deprecated but widely supported and simpler than AudioWorklet
            this.processor = this.audioContext.createScriptProcessor(4096, 1, 1);

            this.audioChunks = [];
            this.isRecording = true;
            this.startTime = Date.now();

            this.processor.onaudioprocess = function (event) {
                if (!this.isRecording) {
                    return;
                }

                var inputData = event.inputBuffer.getChannelData(0);
                var samples = new Float32Array(inputData.length);
                samples.set(inputData);
                this.audioChunks.push(samples);

                // Calculate RMS volume level (0-1)
                var sum = 0;
                for (var i = 0; i < inputData.length; i++) {
                    sum += inputData[i] * inputData[i];
                }
                this._currentVolume = Math.sqrt(sum / inputData.length);

                // Check max duration
                var elapsed = (Date.now() - this.startTime) / 1000;
                if (elapsed >= this.maxDuration) {
                    this._emit('maxDurationReached', { durationSeconds: elapsed });
                    this.stop();
                }
            }.bind(this);

            this.sourceNode.connect(this.processor);
            this.processor.connect(this.audioContext.destination);

            // Emit volume events periodically (~100ms)
            this._volumeTimer = setInterval(function () {
                if (this.isRecording) {
                    this._emit('volume', { level: this._currentVolume });
                }
            }.bind(this), 100);

            this._emit('started', { sampleRate: 16000 });
        } catch (err) {
            this._cleanup();
            this._emit('error', {
                type: 'initialization_failed',
                message: 'Failed to initialize audio recording: ' + err.message
            });
        }
    }

    /**
     * Stop recording. Converts collected Float32 chunks into a 16-bit PCM WAV file
     * encoded as base64.
     * @returns {string|null} Base64-encoded WAV data, or null if not recording.
     */
    stop() {
        if (!this.isRecording) {
            return null;
        }

        this.isRecording = false;
        var durationSeconds = (Date.now() - this.startTime) / 1000;

        // Merge all Float32 chunks into a single array
        var totalLength = 0;
        for (var i = 0; i < this.audioChunks.length; i++) {
            totalLength += this.audioChunks[i].length;
        }

        var mergedFloat32 = new Float32Array(totalLength);
        var offset = 0;
        for (var i = 0; i < this.audioChunks.length; i++) {
            mergedFloat32.set(this.audioChunks[i], offset);
            offset += this.audioChunks[i].length;
        }

        // Convert to 16-bit PCM
        var pcmData = this._floatTo16BitPCM(mergedFloat32);

        // Create WAV buffer and convert to base64
        var wavBuffer = this._createWavBuffer(pcmData);
        var audioBase64 = this._arrayBufferToBase64(wavBuffer);

        this._cleanup();

        this._emit('stopped', {
            audioBase64: audioBase64,
            durationSeconds: durationSeconds
        });

        return audioBase64;
    }

    /**
     * Convert Float32 samples (range -1.0 to 1.0) to Int16 PCM.
     * @param {Float32Array} float32Array
     * @returns {Int16Array}
     */
    _floatTo16BitPCM(float32Array) {
        var pcm = new Int16Array(float32Array.length);
        for (var i = 0; i < float32Array.length; i++) {
            var sample = float32Array[i];
            // Clamp to [-1, 1]
            if (sample > 1.0) {
                sample = 1.0;
            } else if (sample < -1.0) {
                sample = -1.0;
            }
            // Scale to 16-bit signed integer range
            pcm[i] = sample < 0
                ? sample * 0x8000
                : sample * 0x7FFF;
        }
        return pcm;
    }

    /**
     * Create a complete WAV file buffer from 16-bit PCM data.
     * WAV header: 44 bytes (RIFF + fmt + data chunks).
     * Format: 1 channel, 16000 Hz sample rate, 16 bits per sample.
     * @param {Int16Array} pcmData
     * @returns {ArrayBuffer}
     */
    _createWavBuffer(pcmData) {
        var sampleRate = 16000;
        var numChannels = 1;
        var bitsPerSample = 16;
        var bytesPerSample = bitsPerSample / 8;
        var blockAlign = numChannels * bytesPerSample;
        var byteRate = sampleRate * blockAlign;
        var dataSize = pcmData.length * bytesPerSample;
        var headerSize = 44;
        var totalSize = headerSize + dataSize;

        var buffer = new ArrayBuffer(totalSize);
        var view = new DataView(buffer);

        // RIFF chunk descriptor
        this._writeString(view, 0, 'RIFF');
        view.setUint32(4, totalSize - 8, true);          // File size minus RIFF header (8 bytes)
        this._writeString(view, 8, 'WAVE');

        // fmt sub-chunk
        this._writeString(view, 12, 'fmt ');
        view.setUint32(16, 16, true);                     // Sub-chunk size (16 for PCM)
        view.setUint16(20, 1, true);                      // Audio format (1 = PCM)
        view.setUint16(22, numChannels, true);             // Number of channels
        view.setUint32(24, sampleRate, true);              // Sample rate
        view.setUint32(28, byteRate, true);                // Byte rate
        view.setUint16(32, blockAlign, true);              // Block align
        view.setUint16(34, bitsPerSample, true);           // Bits per sample

        // data sub-chunk
        this._writeString(view, 36, 'data');
        view.setUint32(40, dataSize, true);                // Data size in bytes

        // Write PCM samples into the buffer after the header
        var pcmOffset = headerSize;
        for (var i = 0; i < pcmData.length; i++) {
            view.setInt16(pcmOffset, pcmData[i], true);
            pcmOffset += 2;
        }

        return buffer;
    }

    /**
     * Write an ASCII string into a DataView at the specified offset.
     * @param {DataView} view
     * @param {number} offset
     * @param {string} str
     */
    _writeString(view, offset, str) {
        for (var i = 0; i < str.length; i++) {
            view.setUint8(offset + i, str.charCodeAt(i));
        }
    }

    /**
     * Convert an ArrayBuffer to a base64-encoded string.
     * Processes in chunks to avoid call stack overflow on large buffers.
     * @param {ArrayBuffer} buffer
     * @returns {string}
     */
    _arrayBufferToBase64(buffer) {
        var bytes = new Uint8Array(buffer);
        var binary = '';
        var chunkSize = 8192;
        for (var i = 0; i < bytes.length; i += chunkSize) {
            var end = i + chunkSize;
            if (end > bytes.length) {
                end = bytes.length;
            }
            var slice = bytes.subarray(i, end);
            binary += String.fromCharCode.apply(null, slice);
        }
        return btoa(binary);
    }

    /**
     * Clean up all audio resources (stream tracks, processor, context, timers).
     */
    _cleanup() {
        if (this._volumeTimer) {
            clearInterval(this._volumeTimer);
            this._volumeTimer = null;
        }

        if (this.processor) {
            this.processor.onaudioprocess = null;
            try {
                this.processor.disconnect();
            } catch (e) {
                // Ignore disconnect errors on already-disconnected nodes
            }
            this.processor = null;
        }

        if (this.sourceNode) {
            try {
                this.sourceNode.disconnect();
            } catch (e) {
                // Ignore disconnect errors on already-disconnected nodes
            }
            this.sourceNode = null;
        }

        if (this.mediaStream) {
            var tracks = this.mediaStream.getTracks();
            for (var i = 0; i < tracks.length; i++) {
                tracks[i].stop();
            }
            this.mediaStream = null;
        }

        if (this.audioContext) {
            try {
                this.audioContext.close();
            } catch (e) {
                // Ignore close errors if context is already closed
            }
            this.audioContext = null;
        }

        this.audioChunks = [];
        this._currentVolume = 0;
    }

    /**
     * Dispose the recorder and release all resources.
     * Safe to call multiple times.
     */
    dispose() {
        this.isRecording = false;
        this._cleanup();
        this._callbacks = {};
    }
}

window.AudioRecorder = AudioRecorder;
