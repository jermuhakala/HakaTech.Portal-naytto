// =============================================================================
// cmdk.js — Komentopaletti (Cmd/Ctrl + K).
// -----------------------------------------------------------------------------
// Linear/Vercel-tyylinen pikahaku, joka avautuu pikanäppäimellä Ctrl+K.
// Antaa nopean tavan navigoida sivulle, etsiä tikettejä, asiakkaita,
// laskuja ja tietopankin artikkeleita yhdestä paikasta.
// Käyttää /api/search-rajapintaa palvelinhakuun (yli 2 merkin syötteet).
// =============================================================================
(function () {
    'use strict';

    // Käännökset ja navigaatio annetaan globaaleilla muuttujilla layoutista.
    var I18N = window.__cmdkI18n || {};
    var NAV = window.__cmdkNav || []; // [{label, url, icon, kind}]

    // DOM-viittaukset paletin osiin.
    var root = null, input = null, list = null, hintEl = null, overlay = null;
    var items = [];   // näkyvät rivit [{el, action}]
    var groups = [];  // ryhmäotsikot
    var activeIdx = 0; // mikä rivi on tällä hetkellä korostettuna
    var lastFocus = null; // mihin elementtiin fokus palautetaan paletin sulkeutuessa
    var xhr = null;
    var debounceT = null; // odotetaan käyttäjän lopettaa kirjoittamisen ennen API-kutsua

    // Luo paletin DOM-rakenteen ensimmäisellä avauksella, sen jälkeen
    // pidetään se piilossa ja näytetään uudelleen tarvittaessa.
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

    // Avaa paletin näkyviin ja kohdistaa fokuksen hakukenttään.
    function open() {
        ensureDom();
        lastFocus = document.activeElement;
        overlay.classList.add('show');
        root.classList.add('show');
        input.value = '';
        render({}, NAV);
        // Pieni viive jotta animaatio ehtii alkaa ennen fokusointia.
        setTimeout(function () { input.focus(); }, 10);
    }
    // Sulkee paletin ja palauttaa fokuksen sinne missä oltiin avauksen hetkellä.
    function close() {
        if (!root) return;
        overlay.classList.remove('show');
        root.classList.remove('show');
        if (lastFocus && lastFocus.focus) lastFocus.focus();
    }

    // Ajetaan jokaisen näppäilyn jälkeen. Alle 2 merkin haku tehdään
    // pelkästään navigaatiosta paikallisesti, pidempi haku menee palvelimelle
    // 160 ms viiveen jälkeen (debounce), jotta jokaista näppäilyä ei kutsuta API:a.
    function onInput() {
        var q = input.value.trim();
        if (debounceT) clearTimeout(debounceT);
        if (q.length < 2) {
            render({}, fuzzy(NAV, q));
            return;
        }
        debounceT = setTimeout(function () { search(q); }, 160);
    }

    // Näppäimistöohjaus: ↑↓ liikkuu listassa, Enter aktivoi, Esc sulkee.
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

    // Yksinkertainen "fuzzy"-suodatus paikallisille navigaatiokohteille:
    // tarkistaa sisältyykö hakuteksti label:in osana (case-insensitive).
    function fuzzy(navArr, q) {
        if (!q) return navArr;
        var qq = q.toLowerCase();
        return navArr.filter(function (n) {
            return n.label.toLowerCase().indexOf(qq) !== -1;
        });
    }

    // Tekee palvelinhaun /api/search-rajapintaan. Aiempi käynnissä oleva
    // pyyntö perutaan AbortControllerilla, jotta vain uusin tulos näytetään.
    function search(q) {
        if (xhr) xhr.abort?.();
        var navFiltered = fuzzy(NAV, q);
        xhr = new AbortController();
        fetch('/api/search?q=' + encodeURIComponent(q) + '&take=5', { signal: xhr.signal, headers: { 'Accept': 'application/json' } })
            .then(function (r) { return r.ok ? r.json() : Promise.reject(r.status); })
            .then(function (data) { render(data, navFiltered); })
            .catch(function () { render({}, navFiltered); });
    }

    // Renderöi koko listan annettujen hakutulosten ja navigaatiokohteiden pohjalta.
    // Tulokset näytetään ryhmissä (Navigaatio / Tiketit / Laskut / Asiakkaat / Tietopankki).
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

    // Julkinen API — paletti voidaan avata/sulkea myös ohjelmallisesti.
    window.cmdkOpen = open;
    window.cmdkClose = close;

    // Globaali pikanäppäin: Ctrl/Cmd + K avaa paletin mistä tahansa.
    document.addEventListener('keydown', function (e) {
        var isKey = (e.key === 'k' || e.key === 'K');
        if ((e.ctrlKey || e.metaKey) && isKey) {
            e.preventDefault();
            open();
        }
    });

    // Topbar-painikkeen kytkentä: klikkaus avaa paletin.
    document.addEventListener('DOMContentLoaded', function () {
        var btn = document.getElementById('cmdkTrigger');
        if (btn) {
            btn.disabled = false;
            btn.addEventListener('click', open);
        }
    });
})();
