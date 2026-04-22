// HakaTech Portal – Toast system
// Usage: window.toast("Saved!", "success")  |  tone = info|success|warning|danger
(function () {
    'use strict';

    var TONE_ICONS = {
        success: 'bi-check-circle-fill',
        danger:  'bi-exclamation-triangle-fill',
        error:   'bi-exclamation-triangle-fill',
        warning: 'bi-exclamation-circle-fill',
        info:    'bi-info-circle-fill'
    };

    function host() {
        var el = document.getElementById('toastHost');
        if (!el) {
            el = document.createElement('div');
            el.id = 'toastHost';
            el.setAttribute('aria-live', 'polite');
            el.setAttribute('aria-atomic', 'true');
            document.body.appendChild(el);
        }
        return el;
    }

    function toast(msg, tone, opts) {
        if (!msg) return;
        tone = (tone || 'info').toLowerCase();
        if (tone === 'error') tone = 'danger';
        opts = opts || {};
        var timeout = opts.timeout != null ? opts.timeout : (tone === 'danger' ? 6000 : 4000);

        var el = document.createElement('div');
        el.className = 'toast-lnr tone-' + tone;
        el.setAttribute('role', tone === 'danger' ? 'alert' : 'status');

        var icon = document.createElement('i');
        icon.className = 'bi ' + (TONE_ICONS[tone] || TONE_ICONS.info) + ' toast-icon';
        el.appendChild(icon);

        var body = document.createElement('div');
        body.textContent = msg;
        body.style.flex = '1';
        el.appendChild(body);

        var close = document.createElement('button');
        close.type = 'button';
        close.className = 'toast-close';
        close.setAttribute('aria-label', (window.__i18n && window.__i18n.toastDismiss) || 'Close');
        close.innerHTML = '<i class="bi bi-x"></i>';
        close.addEventListener('click', function () { dismiss(el); });
        el.appendChild(close);

        host().appendChild(el);
        // trigger enter
        requestAnimationFrame(function () { el.classList.add('show'); });

        if (timeout > 0) {
            setTimeout(function () { dismiss(el); }, timeout);
        }
        return el;
    }

    function dismiss(el) {
        if (!el || el.__leaving) return;
        el.__leaving = true;
        el.classList.remove('show');
        el.classList.add('leaving');
        setTimeout(function () { if (el.parentNode) el.parentNode.removeChild(el); }, 220);
    }

    // Drain seeded toasts from _ToastHost.cshtml
    function drainSeed() {
        if (!window.__toasts || !window.__toasts.length) return;
        window.__toasts.forEach(function (t) { toast(t.msg, t.tone); });
        window.__toasts = [];
    }

    // Public API
    window.toast = toast;
    window.dismissToast = dismiss;

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', drainSeed);
    } else {
        drainSeed();
    }
})();
