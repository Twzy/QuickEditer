window.quickEditer = window.quickEditer || {};

window.quickEditer.espFlasher = (function () {
    let transport = null;
    let loader = null;
    let dotNetRef = null;

    function log(text, isError) {
        if (dotNetRef) dotNetRef.invokeMethodAsync('OnFlashLog', text, !!isError);
    }

    function progress(percent) {
        if (dotNetRef) dotNetRef.invokeMethodAsync('OnFlashProgress', percent);
    }

    function bindDotNet(ref) {
        dotNetRef = ref;
    }

    async function flash(files, baudRate, eraseAll) {
        try {
            if (!('serial' in navigator)) {
                log('当前浏览器不支持 Web Serial API', true);
                return false;
            }

            const mod = await import('/lib/esptool/esptool.js');
            const { ESPLoader, Transport } = mod;
            if (!ESPLoader || !Transport) {
                log('esptool-js 加载失败，请确认 /lib/esptool/esptool.js 为浏览器 bundle', true);
                return false;
            }

            log('请选择串口...');
            const port = await navigator.serial.requestPort();

            transport = new Transport(port, true);
            loader = new ESPLoader({
                transport: transport,
                baudrate: baudRate || 115200,
                terminal: {
                    clean: () => { },
                    writeLine: (data) => log(data),
                    write: (data) => { }
                }
            });

            log('连接芯片...');
            const chip = await loader.main();
            log('芯片：' + chip);

            const fileArray = files.map(f => ({
                data: f.data,
                address: f.address
            }));

            log('开始烧录，共 ' + fileArray.length + ' 个文件...');

            await loader.writeFlash({
                fileArray: fileArray,
                flashMode: 'dio',
                flashFreq: '40m',
                flashSize: '4MB',
                eraseAll: !!eraseAll,
                compress: true,
                reportProgress: (fileIndex, written, total) => {
                    const percent = Math.round((written / total) * 100);
                    progress(percent);
                }
            });

            log('烧录完成，复位设备...');
            await loader.after('hard_reset');
            log('烧录成功');
            return true;
        } catch (e) {
            log('烧录失败：' + (e.message || e), true);
            return false;
        } finally {
            try {
                if (transport) await transport.disconnect();
            } catch (e) { }
            transport = null;
            loader = null;
        }
    }

    return { bindDotNet, flash };
})();