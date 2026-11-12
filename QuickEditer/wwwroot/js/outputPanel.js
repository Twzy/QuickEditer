// 文件路径：wwwroot/js/outputPanel.js
window.quickEditer = window.quickEditer || {};

window.quickEditer.scrollOutputToBottom = function () {
    const el = document.querySelector('.output-body');
    if (el) el.scrollTop = el.scrollHeight;
};