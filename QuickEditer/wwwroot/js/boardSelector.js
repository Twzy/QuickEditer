 //文件路径：wwwroot/js/boardSelector.js


window.quickEditer = window.quickEditer || {};

window.quickEditer.registerBoardMenuClose = function (dotNetRef) {
    if (window.quickEditer._boardMenuHandler) {
        document.removeEventListener('mousedown', window.quickEditer._boardMenuHandler, true);
    }

    window.quickEditer._boardMenuHandler = function (e) {
        if (e.target.closest('.board-selector')) return;
        document.removeEventListener('mousedown', window.quickEditer._boardMenuHandler, true);
        window.quickEditer._boardMenuHandler = null;
        dotNetRef.invokeMethodAsync('CloseMenu');
    };

    document.addEventListener('mousedown', window.quickEditer._boardMenuHandler, true);
};

window.quickEditer.unregisterBoardMenuClose = function () {
    if (window.quickEditer._boardMenuHandler) {
        document.removeEventListener('mousedown', window.quickEditer._boardMenuHandler, true);
        window.quickEditer._boardMenuHandler = null;
    }
};