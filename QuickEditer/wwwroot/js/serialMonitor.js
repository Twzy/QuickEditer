window.quickEditer = window.quickEditer || {};

window.quickEditer.serialMonitor = (function () {
    let port = null;
    let reader = null;
    let writer = null;
    let keepReading = false;
    let textDecoder = new TextDecoder();
    let lineBuffer = '';
    let chartData = [];
    let chartSeries = 1;
    let maxPoints = 200;
    let canvas = null;
    let ctx = null;
    let dotNetRef = null;
    let isNumericMode = false;

    function log(text, isError) {
        if (dotNetRef) dotNetRef.invokeMethodAsync('OnSerialLog', text, !!isError);
    }

    function updateStatus(connected, portName) {
        if (dotNetRef) dotNetRef.invokeMethodAsync('OnSerialStatus', connected, portName || '');
    }

    async function connect(baudRate) {
        if (!('serial' in navigator)) {
            log('当前浏览器不支持 Web Serial API', true);
            return false;
        }
        try {
            port = await navigator.serial.requestPort();
            await port.open({ baudRate: baudRate });
            writer = port.writable.getWriter();
            updateStatus(true, '');
            log('已连接，波特率 ' + baudRate);
            keepReading = true;
            readLoop();
            return true;
        } catch (e) {
            log('连接失败：' + e.message, true);
            return false;
        }
    }

    async function disconnect() {
        keepReading = false;
        try {
            if (reader) {
                await reader.cancel();
                reader = null;
            }
            if (writer) {
                try { writer.releaseLock(); } catch (e) { }
                writer = null;
            }
            if (port) {
                await port.close();
                port = null;
            }
            updateStatus(false, '');
            log('已断开');
        } catch (e) {
            log('断开失败：' + e.message, true);
        }
    }

    async function send(text, appendNewline) {
        if (!writer) {
            log('串口未连接', true);
            return false;
        }
        try {
            const data = appendNewline ? text + '\r\n' : text;
            const bytes = new TextEncoder().encode(data);
            await writer.write(bytes);
            log('发送：' + text);
            return true;
        } catch (e) {
            log('发送失败：' + e.message, true);
            return false;
        }
    }

    async function readLoop() {
        while (port && port.readable && keepReading) {
            reader = port.readable.getReader();
            try {
                while (keepReading) {
                    const { value, done } = await reader.read();
                    if (done) break;
                    if (value) {
                        const text = textDecoder.decode(value, { stream: true });
                        lineBuffer += text;
                        let idx;
                        while ((idx = lineBuffer.indexOf('\n')) >= 0) {
                            let line = lineBuffer.substring(0, idx).replace('\r', '');
                            lineBuffer = lineBuffer.substring(idx + 1);
                            handleLine(line);
                        }
                    }
                }
            } catch (e) {
                if (keepReading) log('读取错误：' + e.message, true);
            } finally {
                try { reader.releaseLock(); } catch (e) { }
                reader = null;
            }
        }
    }

    function handleLine(line) {
        if (!line) return;

        if (isNumericMode) {
            const nums = line.split(/[\s,;]+/).map(s => parseFloat(s)).filter(n => !isNaN(n));
            if (nums.length > 0) {
                chartSeries = nums.length;
                chartData.push(nums);
                if (chartData.length > maxPoints) chartData.shift();
                drawChart();
                return;
            }
        }

        if (dotNetRef) dotNetRef.invokeMethodAsync('OnSerialLine', line);
    }

    function drawChart() {
        if (!canvas) return;
        if (!ctx) ctx = canvas.getContext('2d');
        const w = canvas.width;
        const h = canvas.height;
        ctx.clearRect(0, 0, w, h);

        if (chartData.length < 2) return;

        const series = chartSeries;
        let min = Infinity, max = -Infinity;
        for (const row of chartData) {
            for (const v of row) {
                if (v < min) min = v;
                if (v > max) max = v;
            }
        }
        if (min === max) { min -= 1; max += 1; }

        const pad = 30;
        const plotW = w - pad * 2;
        const plotH = h - pad * 2;

        ctx.strokeStyle = '#3c3c3c';
        ctx.lineWidth = 1;
        ctx.beginPath();
        ctx.moveTo(pad, pad);
        ctx.lineTo(pad, h - pad);
        ctx.lineTo(w - pad, h - pad);
        ctx.stroke();

        const colors = ['#569cd6', '#4ec9b0', '#dcdcaa', '#f48771', '#9cdcfe', '#ce9178'];

        for (let s = 0; s < series; s++) {
            ctx.strokeStyle = colors[s % colors.length];
            ctx.lineWidth = 1.5;
            ctx.beginPath();
            for (let i = 0; i < chartData.length; i++) {
                const v = chartData[i][s];
                if (v === undefined) continue;
                const x = pad + (i / (maxPoints - 1)) * plotW;
                const y = h - pad - ((v - min) / (max - min)) * plotH;
                if (i === 0) ctx.moveTo(x, y);
                else ctx.lineTo(x, y);
            }
            ctx.stroke();
        }

        ctx.fillStyle = '#8a8a8a';
        ctx.font = '10px monospace';
        ctx.fillText(max.toFixed(2), 2, pad + 4);
        ctx.fillText(min.toFixed(2), 2, h - pad);
    }

    function setChartCanvas(c) {
        canvas = c;
        ctx = null;
        resizeChart();
    }

    function resizeChart() {
        if (!canvas) return;
        const rect = canvas.getBoundingClientRect();
        canvas.width = rect.width;
        canvas.height = rect.height;
        drawChart();
    }

    function clear() {
        lineBuffer = '';
        chartData = [];
        drawChart();
    }

    function setMode(numeric) {
        isNumericMode = numeric;
        drawChart();
    }

    function bindDotNet(ref) {
        dotNetRef = ref;
    }
    return {
        connect, disconnect, clear, setMode, bindDotNet,
        setChartCanvas, resizeChart, send,
        isConnected: function () {
            return !!port;
        }
    };
})();