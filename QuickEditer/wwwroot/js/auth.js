//文件路径：wwwroot/js/auth.js

window.quickEditer = window.quickEditer || {};

window.quickEditer.setAuthCookie = function (token, days) {
    try {
        const expireDays = days && days > 0 ? days : 1;
        const expireMs = Date.now() + expireDays * 24 * 60 * 60 * 1000;
        localStorage.setItem('qe_auth_token', token);
        localStorage.setItem('qe_auth_expire', String(expireMs));

        const expires = new Date(expireMs).toUTCString();
        document.cookie = `qe_auth=${encodeURIComponent(token)}; expires=${expires}; path=/; SameSite=Lax`;
    } catch (e) { }
};

window.quickEditer.getAuthCookie = function () {
    try {
        const expire = parseInt(localStorage.getItem('qe_auth_expire') || '0', 10);
        if (expire > 0 && Date.now() > expire) {
            localStorage.removeItem('qe_auth_token');
            localStorage.removeItem('qe_auth_expire');
            return "";
        }
        return localStorage.getItem('qe_auth_token') || "";
    } catch (e) { return ""; }
};

window.quickEditer.clearAuthCookie = function () {
    try {
        localStorage.removeItem('qe_auth_token');
        localStorage.removeItem('qe_auth_expire');
        document.cookie = 'qe_auth=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/';
    } catch (e) { }
};

window.quickEditer.authFetch = async function (url, options) {
    options = options || {};
    options.headers = options.headers || {};
    const token = window.quickEditer.getAuthCookie();
    if (token) {
        options.headers['X-Auth-Token'] = token;
    }
    return fetch(url, options);
};