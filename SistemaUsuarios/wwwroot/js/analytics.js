/*!
 * Agent Tools Analytics — camada centralizada de eventos GA4/GTM
 *
 * Uso:
 *   window.agentToolsAnalytics.init(gtmId, enabled, ga4Id)
 *   window.agentToolsAnalytics.track(eventName, params)
 *   window.agentToolsAnalytics.accept()
 *   window.agentToolsAnalytics.deny()
 *   window.agentToolsAnalytics.showBanner()   // reabre o banner
 *   window.agentToolsAnalytics.debug = true   // habilita logs (dev only)
 */
(function () {
    'use strict';

    var STORAGE_KEY = 'at_consent_v1';

    // dataLayer e Consent Mode v2 devem ser configurados ANTES do GTM carregar
    window.dataLayer = window.dataLayer || [];
    function _gtag() { window.dataLayer.push(arguments); }

    // Padrão: tudo negado até o usuário decidir
    _gtag('consent', 'default', {
        ad_storage: 'denied',
        analytics_storage: 'denied',
        ad_user_data: 'denied',
        ad_personalization: 'denied',
        wait_for_update: 500
    });

    // ── Utilitários ──────────────────────────────────────────────────────────────

    function countRange(n) {
        n = parseInt(n, 10) || 0;
        if (n <= 1) return '1';
        if (n <= 3) return '2_to_3';
        if (n <= 5) return '4_to_5';
        return '6_plus';
    }

    function cleanParams(obj) {
        var out = {};
        if (!obj) return out;
        Object.keys(obj).forEach(function (k) {
            var v = obj[k];
            if (v !== null && v !== undefined && v !== '') out[k] = v;
        });
        return out;
    }

    // ── Singleton ────────────────────────────────────────────────────────────────

    var analytics = {
        debug: false,
        _enabled: false,
        _gtmId: null,
        _ga4Id: null,
        _gtmLoaded: false,
        _ga4Loaded: false,
        _firedKeys: {},

        // ── helpers internos ───────────────────────────────────────────────────

        _log: function () {
            if (this.debug && window.console && console.log) {
                var args = Array.prototype.slice.call(arguments);
                args.unshift('[AT Analytics]');
                console.log.apply(console, args);
            }
        },

        _getConsent: function () {
            try { return localStorage.getItem(STORAGE_KEY); } catch (e) { return null; }
        },

        _setConsent: function (v) {
            try { localStorage.setItem(STORAGE_KEY, v); } catch (e) {}
        },

        _updateConsentMode: function (analyticsStorage) {
            _gtag('consent', 'update', { analytics_storage: analyticsStorage });
        },

        _loadGTM: function () {
            if (this._gtmLoaded || !this._gtmId) return;
            this._gtmLoaded = true;
            window.dataLayer.push({ 'gtm.start': new Date().getTime(), event: 'gtm.js' });
            var s = document.createElement('script');
            s.async = true;
            s.src = 'https://www.googletagmanager.com/gtm.js?id=' + this._gtmId;
            var f = document.getElementsByTagName('script')[0];
            if (f && f.parentNode) f.parentNode.insertBefore(s, f);
            this._log('GTM carregado', this._gtmId);
        },

        _loadGA4: function () {
            if (this._ga4Loaded || !this._ga4Id) return;
            this._ga4Loaded = true;
            var s = document.createElement('script');
            s.async = true;
            s.src = 'https://www.googletagmanager.com/gtag/js?id=' + this._ga4Id;
            var f = document.getElementsByTagName('script')[0];
            if (f && f.parentNode) f.parentNode.insertBefore(s, f);
            _gtag('js', new Date());
            _gtag('config', this._ga4Id, { send_page_view: true });
            this._log('GA4 carregado', this._ga4Id);
        },

        _showBannerEl: function () {
            var el = document.getElementById('at-cookie-banner');
            if (el) el.removeAttribute('hidden');
        },

        _hideBanner: function () {
            var el = document.getElementById('at-cookie-banner');
            if (el) el.setAttribute('hidden', '');
        },

        // ── API pública ────────────────────────────────────────────────────────

        init: function (gtmId, enabled, ga4Id) {
            this._enabled = !!enabled && !!gtmId;
            this._gtmId = gtmId || null;
            this._ga4Id = (ga4Id && ga4Id !== '') ? ga4Id : null;

            if (!this._enabled) {
                this._log('desabilitado (sem GTM ID ou fora de produção)');
                return;
            }

            var consent = this._getConsent();
            var self = this;

            if (consent === 'granted') {
                this._updateConsentMode('granted');
            } else if (consent === 'denied') {
                this._updateConsentMode('denied');
            } else {
                // Sem decisão: exibe banner após 1.5s
                var showFn = function () {
                    setTimeout(function () { self._showBannerEl(); }, 1500);
                };
                if (document.readyState === 'loading') {
                    document.addEventListener('DOMContentLoaded', showFn);
                } else {
                    showFn();
                }
            }

            // Carrega GTM e GA4 (GA4 direto garante coleta mesmo sem tag no GTM configurada)
            this._loadGTM();
            this._loadGA4();
            this.captureUtm();
            this._log('init', { enabled: true, gtmId: this._gtmId, ga4Id: this._ga4Id, consent: consent || 'pending' });
        },

        hasConsent: function () {
            return this._getConsent() === 'granted';
        },

        accept: function () {
            this._setConsent('granted');
            this._updateConsentMode('granted');
            this._hideBanner();
            this._log('consentimento aceito');
        },

        deny: function () {
            this._setConsent('denied');
            this._updateConsentMode('denied');
            this._hideBanner();
            this._log('consentimento recusado');
        },

        showBanner: function () {
            this._showBannerEl();
        },

        revoke: function () {
            try { localStorage.removeItem(STORAGE_KEY); } catch (e) {}
            this._log('consentimento revogado');
        },

        // ── Rastreamento de eventos ────────────────────────────────────────────

        track: function (eventName, params) {
            if (!this._enabled || !eventName) return;
            var p = cleanParams(params);
            // Envia via dataLayer (GTM) e diretamente via gtag (GA4)
            window.dataLayer = window.dataLayer || [];
            window.dataLayer.push({ event: eventName, eventParams: p });
            if (this._ga4Id) _gtag('event', eventName, p);
            this._log('track', eventName, p);
        },

        trackOnce: function (key, eventName, params) {
            if (this._firedKeys[key]) return;
            this._firedKeys[key] = true;
            this.track(eventName, params);
        },

        // ── Captura de UTMs ────────────────────────────────────────────────────

        captureUtm: function () {
            try {
                var p = new URLSearchParams(window.location.search);
                var keys = ['utm_source', 'utm_medium', 'utm_campaign', 'utm_content', 'utm_term'];
                var data = {}, found = false;
                keys.forEach(function (k) {
                    var v = p.get(k);
                    if (v) { data[k] = v; found = true; }
                });
                if (found) {
                    sessionStorage.setItem('at_utm', JSON.stringify(data));
                    this._log('UTMs capturados', data);
                }
            } catch (e) {}
        },

        getUtm: function () {
            try { return JSON.parse(sessionStorage.getItem('at_utm') || '{}'); } catch (e) { return {}; }
        },

        // ── Helpers expostos às views ──────────────────────────────────────────

        countRange: countRange,

        // Listener global de CTAs via data-analytics-cta
        _initCtaTracking: function () {
            var self = this;
            document.addEventListener('click', function (e) {
                var el = e.target.closest('[data-analytics-cta]');
                if (!el) return;
                self.track('cta_click', {
                    cta_name: el.getAttribute('data-analytics-cta') || '',
                    cta_location: el.getAttribute('data-analytics-loc') || '',
                    destination: el.getAttribute('data-analytics-dst') || ''
                });
            }, { passive: true });
        }
    };

    window.agentToolsAnalytics = analytics;

    // Ativa rastreamento de CTAs assim que o DOM estiver pronto
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', function () { analytics._initCtaTracking(); });
    } else {
        analytics._initCtaTracking();
    }
})();
