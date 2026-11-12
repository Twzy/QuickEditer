// 文件路径：wwwroot/js/resizable.js

window.quickEditer = window.quickEditer || {};

const STORAGE_SIDEBAR = 'qe.sidebarWidth';
const STORAGE_OUTPUT = 'qe.outputHeight';
const MIN_SIDEBAR = 180;
const MAX_SIDEBAR = 560;
const MIN_OUTPUT = 80;
const MAX_OUTPUT_RATIO = 0.75;

window.quickEditer.initResizable = function (dotNetRef) {
    const sidebar = document.querySelector('.project-sidebar');
    const splitterV = document.querySelector('.splitter-vertical');
    const splitterH = document.querySelector('.splitter-horizontal');
    const editorAndOutput = document.querySelector('.editor-and-output');
    const outputPanel = document.querySelector('.bottom-panel');

    if (!sidebar || !splitterV || !editorAndOutput) return;

    // 恢复侧栏宽度
    const savedSidebar = parseInt(localStorage.getItem(STORAGE_SIDEBAR) || '0', 10);
    if (savedSidebar >= MIN_SIDEBAR) {
        sidebar.style.width = savedSidebar + 'px';
        sidebar.style.minWidth = savedSidebar + 'px';
    }

    // 左右拖动
    splitterV.onmousedown = function (e) {
        e.preventDefault();
        const startX = e.clientX;
        const startW = sidebar.getBoundingClientRect().width;
        document.body.style.cursor = 'col-resize';
        document.body.style.userSelect = 'none';
        sidebar.style.transition = 'none';

        function onMove(ev) {
            let w = startW + (ev.clientX - startX);
            w = Math.max(MIN_SIDEBAR, Math.min(MAX_SIDEBAR, w));
            sidebar.style.width = w + 'px';
            sidebar.style.minWidth = w + 'px';
        }
        function onUp() {
            document.body.style.cursor = '';
            document.body.style.userSelect = '';
            sidebar.style.transition = '';
            document.removeEventListener('mousemove', onMove);
            document.removeEventListener('mouseup', onUp);
            const finalW = sidebar.getBoundingClientRect().width;
            localStorage.setItem(STORAGE_SIDEBAR, String(Math.round(finalW)));
            if (dotNetRef) dotNetRef.invokeMethodAsync('OnLayoutChanged', Math.round(finalW), null);
        }
        document.addEventListener('mousemove', onMove);
        document.addEventListener('mouseup', onUp);
    };

    // 上下拖动：输出面板可见时才绑定
    if (splitterH && outputPanel) {
        const savedOutput = parseInt(localStorage.getItem(STORAGE_OUTPUT) || '0', 10);
        if (savedOutput >= MIN_OUTPUT) {
            outputPanel.style.height = savedOutput + 'px';
        }

        splitterH.onmousedown = function (e) {
            e.preventDefault();
            const startY = e.clientY;
            const startH = outputPanel.getBoundingClientRect().height;
            const containerH = editorAndOutput.getBoundingClientRect().height;
            document.body.style.cursor = 'row-resize';
            document.body.style.userSelect = 'none';

            // 拖动期间禁用过渡，避免高度滞后鼠标
            outputPanel.style.transition = 'none';

            // 拖动期间屏蔽编辑器区指针事件，减少 Monaco 重排卡顿
            const editorArea = editorAndOutput.querySelector('.editor-area');
            if (editorArea) editorArea.style.pointerEvents = 'none';

            function onMove(ev) {
                let h = startH - (ev.clientY - startY);
                const maxH = Math.round(containerH * MAX_OUTPUT_RATIO);
                h = Math.max(MIN_OUTPUT, Math.min(maxH, h));
                outputPanel.style.height = h + 'px';
            }
            function onUp() {
                document.body.style.cursor = '';
                document.body.style.userSelect = '';
                outputPanel.style.transition = '';
                if (editorArea) editorArea.style.pointerEvents = '';
                document.removeEventListener('mousemove', onMove);
                document.removeEventListener('mouseup', onUp);
                const finalH = outputPanel.getBoundingClientRect().height;
                localStorage.setItem(STORAGE_OUTPUT, String(Math.round(finalH)));
                if (dotNetRef) dotNetRef.invokeMethodAsync('OnLayoutChanged', null, Math.round(finalH));
            }
            document.addEventListener('mousemove', onMove);
            document.addEventListener('mouseup', onUp);
        };
    }
};