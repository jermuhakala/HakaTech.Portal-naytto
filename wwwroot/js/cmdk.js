// HakaTech Portal – Command palette (Ctrl/Cmd + K)
(function () {
    'use strict';
    var I18N = window.__cmdkI18n || {};
    var NAV = window.__cmdkNav || []; // [{label, url, icon, kind}]

    var root = null, input = null, list = null, hintEl = null, overlay = null;
    var items = [];   // flat {el, action}
    var groups = [];  // {title, items}
    var activeIdx = 0;
    var lastFocus = null;
    var xhr = null;
    var debounceT = null;

    function ensureDom() {
        if (root) return;
        overlay = document.createElement('div');
        overlay.className = 'cmdk-overlay';
        overlay.addEventListener('click', close);

        root = document.createElement('div');
        root.className = 'cmdk';
        root.setAttribute('role', 'dialog');
        root.setAttribute('aria-modal', 'true');
        root.setAttribute('aria-label', I18N.placeholder || 'Search');

        var head = document.createElement('div');
        head.className = 'cmdk-head';
        var icon = document.createElement('i');
        icon.className = 'bi bi-search';
        input = document.createElement('input');
        input.type = 'text';
        input.className = 'cmdk-input';
        input.autocomplete = 'off';
        input.spellcheck = false;
        input.placeholder = I18N.placeholder || 'Search…';
        input.setAttribute('aria-autocomplete', 'list');
        head.appendChild(icon);
        head.appendChild(input);

        list = document.createElement('div');
        list.className = 'cmdk-list';

        hintEl = document.createElement('div');
        hintEl.className = 'cmdk-hint';
        hintEl.textContent = I18N.keyboardHint || '↑↓ Enter Esc';

        root.appendChild(head);
        root.appendChild(list);
        root.appendChild(hintEl);

        document.body.appendChild(overlay);
        document.body.appendChild(root);

        input.addEventListener('input', onInput);
        input.addEventListener('keydown', onKey);
    }

    function open() {
        ensureDom();
        lastFocus = document.activeElement;
        overlay.classList.add('show');
        root.classList.add('show');
        input.value = '';
        render({}, NAV);
        setTimeout(function () { input.focus(); }, 10);
    }
    function close() {
        if (!root) return;
        overlay.classList.remove('show');
        root.classList.remove('show');
        if (lastFocus && lastFocus.focus) lastFocus.focus();
    }

    function onInput() {
        var q = input.value.trim();
        if (debounceT) clearTimeout(debounceT);
        if (q.length < 2) {
            render({}, fuzzy(NAV, q));
            return;
        }
        debounceT = setTimeout(function () { search(q); }, 160);
    }

    function onKey(e) {
        if (e.key === 'Escape') { e.preventDefault(); close(); return; }
        if (e.key === 'ArrowDown') { e.preventDefault(); move(1); return; }
        if (e.key === 'ArrowUp')   { e.preventDefault(); move(-1); return; }
        if (e.key === 'Enter')     { e.preventDefault(); activate(); return; }
    }
    function move(delta) {
        if (!items.length) return;
        items[activeIdx]?.el.classList.remove('is-active');
        activeIdx = (activeIdx + delta + items.length) % items.length;
        items[activeIdx].el.classList.add('is-active');
        items[activeIdx].el.scrollIntoView({ block: 'nearest' });
    }
    function activate() {
        if (!items.length) return;
        var it = items[activeIdx];
        close();
        if (it.action) it.action();
    }

    function fuzzy(navArr, q) {
        if (!q) return navArr;
        var qq = q.toLowerCase();
        return navArr.filter(function (n) {
            return n.label.toLowerCase().indexOf(qq) !== -1;
        });
    }

    function search(q) {
        if (xhr) xhr.abort?.();
        var navFiltered = fuzzy(NAV, q);
        xhr = new AbortController();
        fetch('/api/search?q=' + encodeURIComponent(q) + '&take=5', { signal: xhr.signal, headers: { 'Accept': 'application/json' } })
            .then(function (r) { return r.ok ? r.json() : Promise.reject(r.status); })
            .then(function (data) { render(data, navFiltered); })
            .catch(function () { render({}, navFiltered); });
    }

    function render(results, navFiltered) {
        list.innerHTML = '';
        items = [];
        activeIdx = 0;

        addGroup(I18N.groupNav, (navFiltered || []).map(function (n) {
            return { icon: n.icon || 'bi-arrow-right-short', title: n.label, subtitle: null, url: n.url };
        }));
        addGroup(I18N.groupTickets,   (results.tickets   || []).map(fmtHit('bi-ticket-detailed')));
        addGroup(I18N.groupInvoices,  (results.invoices  || []).map(fmtHit('bi-receipt')));
        addGroup(I18N.groupCustomers, (results.customers || []).map(fmtHit('bi-building')));
        addGroup(I18N.groupKB,        (results.kbArticles|| []).map(fmtHit('bi-book')));

        if (!items.length) {
            var em = document.createElement('div');
            em.className = 'cmdk-empty';
            em.textContent = I18N.empty || 'No results.';
            list.appendChild(em);
            return;
        }
        items[0].el.classList.add('is-active');
    }

    function fmtHit(defaultIcon) {
        return function (h) {
            return {
                icon: defaultIcon,
                title: h.title,
                subtitle: h.subtitle || h.status || null,
                url: h.url
            };
        };
    }

    function addGroup(title, rows) {
        if (!rows.length) return;
        var heading = document.createElement('div');
        heading.className = 'cmdk-group-title';
        heading.textContent = title || '';
        list.appendChild(heading);
        rows.forEach(function (r) {
            var it = document.createElement('button');
            it.type = 'button';
            it.className = 'cmdk-item';
            it.setAttribute('role', 'option');
            it.innerHTML =
                '<i class="bi ' + r.icon + '"></i>' +
                '<span class="cmdk-item-title"></span>' +
                (r.subtitle ? '<span class="cmdk-item-sub"></span>' : '');
            it.querySelector('.cmdk-item-title').textContent = r.title;
            if (r.subtitle) it.querySelector('.cmdk-item-sub').textContent = r.subtitle;
            var action = function () { window.location.href = r.url; };
            it.addEventListener('click', function () { close(); action(); });
            it.addEventListener('mouseenter', function () {
                items.forEach(function (x) { x.el.classList.remove('is-active'); });
                it.classList.add('is-active');
                activeIdx = items.findIndex(function (x) { return x.el === it; });
            });
            list.appendChild(it);
            items.push({ el: it, action: action });
        });
    }

    // Public open API
    window.cmdkOpen = open;
    window.cmdkClose = close;

    // Global keybinding
    document.addEventListener('keydown', function (e) {
        var isKey = (e.key === 'k' || e.key === 'K');
        if ((e.ctrlKey || e.metaKey) && isKey) {
            e.preventDefault();
            open();
        }
    });

    // Wire trigger button
    document.addEventListener('DOMContentLoaded', function () {
        var btn = document.getElementById('cmdkTrigger');
        if (btn) {
            btn.disabled = false;
            btn.addEventListener('click', open);
        }
    });
})();
