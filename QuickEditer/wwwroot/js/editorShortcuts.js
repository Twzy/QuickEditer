// 文件路径：wwwroot/js/editorShortcuts.js

window.quickEditer = window.quickEditer || {};

window.quickEditer.bindEditorShortcuts = function (dotNetRef) {
    if (window.quickEditer._editorShortcutCleanup) {
        window.quickEditer._editorShortcutCleanup();
        window.quickEditer._editorShortcutCleanup = null;
    }

    const handler = function (e) {
        if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 's') {
            e.preventDefault();
            if (dotNetRef) {
                dotNetRef.invokeMethodAsync('OnSaveShortcut');
            }
        }
    };

    document.addEventListener('keydown', handler, true);

    window.quickEditer._editorShortcutCleanup = function () {
        document.removeEventListener('keydown', handler, true);
    };
};

window.quickEditer.unbindEditorShortcuts = function () {
    if (window.quickEditer._editorShortcutCleanup) {
        window.quickEditer._editorShortcutCleanup();
        window.quickEditer._editorShortcutCleanup = null;
    }
};

window.quickEditer.registerMenuBarClose = function (dotNetRef) {
    if (window.quickEditer._menuBarHandler) {
        document.removeEventListener('mousedown', window.quickEditer._menuBarHandler, true);
        window.quickEditer._menuBarHandler = null;
    }

    window.quickEditer._menuBarHandler = function (e) {
        if (e.target.closest('.menu-bar')) return;
        document.removeEventListener('mousedown', window.quickEditer._menuBarHandler, true);
        window.quickEditer._menuBarHandler = null;
        dotNetRef.invokeMethodAsync('CloseMenu');
    };

    document.addEventListener('mousedown', window.quickEditer._menuBarHandler, true);
};

window.quickEditer.unregisterMenuBarClose = function () {
    if (window.quickEditer._menuBarHandler) {
        document.removeEventListener('mousedown', window.quickEditer._menuBarHandler, true);
        window.quickEditer._menuBarHandler = null;
    }
};

window.quickEditer.confirmClose = function (fileName) {
    const msg = `"${fileName}" 有未保存的更改。\n\n点击"确定"保存并关闭，点击"取消"返回。`;
    const ok = window.confirm(msg);
    return ok ? "save" : "cancel";
};

window.quickEditer.setTheme = function (theme) {
    document.documentElement.dataset.theme = theme || 'dark';
    try { localStorage.setItem('qe.theme', theme || 'dark'); } catch (e) { }
};

window.quickEditer.pickAndUpload = function (dir, dotNetRef) {
    const input = document.createElement('input');
    input.type = 'file';
    input.multiple = true;
    input.onchange = async () => {
        const files = input.files;
        if (!files || files.length === 0) return;

        const form = new FormData();
        for (const f of files) form.append('files', f, f.name);

        try {
            const resp = await window.quickEditer.authFetch(`/api/file/upload?dir=${encodeURIComponent(dir)}`, {
                method: 'POST',
                body: form
            });
            if (!resp.ok) {
                alert('上传失败：' + resp.status);
                return;
            }
            if (dotNetRef) dotNetRef.invokeMethodAsync('OnUploadCompleted', dir);
        } catch (e) {
            alert('上传失败：' + e);
        }
    };
    input.click();
};

window.quickEditer.bindEditorScroll = function (editorId, dotNetRef) {
    if (window.quickEditer._editorScrollCleanup) {
        window.quickEditer._editorScrollCleanup();
        window.quickEditer._editorScrollCleanup = null;
    }

    const tryBind = function () {
        const monaco = window.monaco;
        if (!monaco || !monaco.editor) {
            setTimeout(tryBind, 300);
            return;
        }

        const editors = typeof monaco.editor.getEditors === 'function' ? monaco.editor.getEditors() : [];
        const editor = editors.find(ed => {
            const dom = ed.getDomNode && ed.getDomNode();
            return dom && dom.id && dom.id.indexOf(editorId) >= 0;
        }) || editors[editors.length - 1];

        if (!editor) {
            setTimeout(tryBind, 300);
            return;
        }

        if (editor._qeScrollBound) return;
        editor._qeScrollBound = true;

        editor.onDidScrollChange((e) => {
            if (!e.scrollTopChanged) return;
            if (dotNetRef)
                dotNetRef.invokeMethodAsync('OnEditorScroll', editor.getScrollTop());
        });
    };

    setTimeout(tryBind, 500);
};

window.quickEditer.setEditorReadOnly = function (editorId, readOnly) {
    if (!window.monaco) return;
    const editors = typeof monaco.editor.getEditors === 'function' ? monaco.editor.getEditors() : [];
    const editor = editors.find(ed => {
        const dom = ed.getDomNode && ed.getDomNode();
        return dom && dom.id && dom.id.indexOf(editorId) >= 0;
    }) || editors[editors.length - 1];
    if (!editor) return;
    editor.updateOptions({ readOnly: readOnly });
};