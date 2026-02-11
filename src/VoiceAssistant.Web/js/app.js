(function() {
    // State machine
    const State = {
        IDLE: 'idle',
        CONNECTING: 'connecting',
        RECORDING: 'recording',
        RECOGNIZING: 'recognizing',
        THINKING: 'thinking',
        PLAYING: 'playing',
        ERROR: 'error'
    };

    // Status text in Chinese
    const StatusText = {
        idle: '就绪',
        connecting: '连接中...',
        recording: '录音中...',
        recognizing: '识别中...',
        thinking: '思考中...',
        playing: '播放中...',
        error: '发生错误'
    };

    let state = State.IDLE;
    let wsClient, recorder, player;
    let currentAssistantText = '';  // accumulates streamed assistant text

    // DOM elements
    const chatHistory = document.getElementById('chat-history');
    const statusEl = document.getElementById('status');
    const btnRecord = document.getElementById('btn-record');

    function setState(newState) {
        state = newState;
        statusEl.textContent = StatusText[state] || state;
        statusEl.className = 'status ' + state;  // for CSS styling

        // Toggle recording class on button
        btnRecord.classList.toggle('recording', state === State.RECORDING);
        btnRecord.disabled = (state === State.CONNECTING || state === State.RECOGNIZING || state === State.THINKING);
    }

    // Add message bubble to chat history
    function addMessage(role, text) {
        // Remove placeholder if present
        const placeholder = chatHistory.querySelector('.placeholder');
        if (placeholder) placeholder.remove();

        const div = document.createElement('div');
        div.className = `message ${role}`;
        const bubble = document.createElement('div');
        bubble.className = 'bubble';
        bubble.textContent = text;
        div.appendChild(bubble);
        chatHistory.appendChild(div);
        chatHistory.scrollTop = chatHistory.scrollHeight;
        return bubble; // return bubble for streaming updates
    }

    // Update the last assistant message bubble (for streaming text)
    let currentAssistantBubble = null;

    function appendAssistantText(textChunk) {
        currentAssistantText += textChunk;
        if (!currentAssistantBubble) {
            currentAssistantBubble = addMessage('assistant', currentAssistantText);
        } else {
            currentAssistantBubble.textContent = currentAssistantText;
        }
        chatHistory.scrollTop = chatHistory.scrollHeight;
    }

    // === Initialize modules ===
    async function init() {
        wsClient = new WebSocketClient();
        recorder = new AudioRecorder();
        player = new AudioPlayer();

        // --- WebSocket events ---
        wsClient.on('connected', () => {
            console.log('Connected to server');
            wsClient.startSession();
            setState(State.CONNECTING);
        });

        wsClient.on('sessionStarted', (data) => {
            console.log('Session started:', data.sessionId);
            setState(State.IDLE);
        });

        wsClient.on('recognitionResult', (data) => {
            if (data.isFinal && data.text) {
                addMessage('user', data.text);
                setState(State.THINKING);
            }
        });

        wsClient.on('assistantTextChunk', (data) => {
            if (state !== State.PLAYING) setState(State.PLAYING);
            appendAssistantText(data.textChunk);
        });

        wsClient.on('audioChunk', (data) => {
            if (state !== State.PLAYING) setState(State.PLAYING);
            player.addChunk(data.audioChunk, data.contentType, data.isComplete);
        });

        wsClient.on('error', (data) => {
            console.error('Server error:', data.code, data.message);
            addMessage('assistant', '\u26A0\uFE0F ' + data.message);
            setState(State.ERROR);
            setTimeout(() => setState(State.IDLE), 3000);
        });

        wsClient.on('disconnected', () => {
            setState(State.ERROR);
        });

        wsClient.on('reconnected', () => {
            wsClient.startSession();
        });

        // --- Recorder events ---
        recorder.on('volume', (level) => {
            // Can add volume indicator here in the future
        });

        recorder.on('maxDurationReached', () => {
            stopRecording();
        });

        recorder.on('error', (err) => {
            console.error('Recorder error:', err);
            setState(State.ERROR);
            setTimeout(() => setState(State.IDLE), 3000);
        });

        // --- Player events ---
        player.on('started', () => {
            setState(State.PLAYING);
        });

        player.on('ended', () => {
            setState(State.IDLE);
        });

        player.on('stopped', () => {
            // Stopped by interruption
        });

        // --- Button events ---
        // Support both click-to-toggle and press-and-hold
        let isHolding = false;

        btnRecord.addEventListener('mousedown', (e) => { handlePressStart(e); });
        btnRecord.addEventListener('mouseup', () => { handlePressEnd(); });
        btnRecord.addEventListener('mouseleave', () => { if (isHolding) handlePressEnd(); });
        btnRecord.addEventListener('touchstart', (e) => { e.preventDefault(); handlePressStart(e); });
        btnRecord.addEventListener('touchend', (e) => { e.preventDefault(); handlePressEnd(); });

        function handlePressStart(e) {
            if (state === State.PLAYING) {
                // Interrupt playback
                player.stop();
            }
            if (state !== State.IDLE && state !== State.ERROR && state !== State.PLAYING) return;
            isHolding = true;
            startRecording();
        }

        function handlePressEnd() {
            if (!isHolding) return;
            isHolding = false;
            if (state === State.RECORDING) {
                stopRecording();
            }
        }

        // Connect
        await wsClient.connect();
    }

    async function startRecording() {
        player.init(); // ensure AudioContext is created on user gesture
        setState(State.RECORDING);
        currentAssistantText = '';
        currentAssistantBubble = null;
        await recorder.start();
    }

    async function stopRecording() {
        const audioBase64 = recorder.stop();
        if (!audioBase64) {
            setState(State.IDLE);
            return;
        }
        setState(State.RECOGNIZING);
        await wsClient.sendAudio(audioBase64);
    }

    // Start the app
    init().catch(err => {
        console.error('Failed to initialize app:', err);
    });
})();
