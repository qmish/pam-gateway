(function () {
    const THEME_KEY = 'pam-theme';
    const LANG_KEY = 'pam-lang';

    const translations = {
        ru: {
            'dashboard.title': 'Обзор системы',
            'dashboard.targets': 'Целевые системы',
            'dashboard.agentsOnline': 'Агенты Online',
            'dashboard.activeSessions': 'Активные сессии',
            'dashboard.pendingRequests': 'Ожидают решения',
            'dashboard.approved': 'Одобрено',
            'dashboard.recordings': 'Записи',
            'dashboard.policies': 'Политики',
            'dashboard.totalAgents': 'Всего агентов',
            'dashboard.recentRequests': 'Последние заявки',
            'dashboard.recentSessions': 'Последние сессии',
            'dashboard.agentStatus': 'Статус агентов',
            'dashboard.lastSeen': 'Последняя активность',
            'dashboard.noRequests': 'Нет заявок',
            'dashboard.noSessions': 'Нет сессий',
            'common.target': 'Цель',
            'common.user': 'Пользователь',
            'common.status': 'Статус',
            'common.date': 'Дата',
            'common.protocol': 'Протокол',
            'common.started': 'Начало',
            'nav.dashboard': 'Dashboard',
            'nav.targets': 'Системы',
            'nav.policies': 'Политики',
            'nav.roles': 'Роли',
            'nav.agents': 'Агенты',
            'nav.sessions': 'Сессии',
            'nav.recordings': 'Записи',
            'nav.requests': 'Заявки',
            'nav.approvals': 'Согласования',
            'nav.approverPanel': 'Панель согласования',
            'theme.toggle': 'Тёмная тема',
            'lang.toggle': 'EN'
        },
        en: {
            'dashboard.title': 'System Overview',
            'dashboard.targets': 'Target Systems',
            'dashboard.agentsOnline': 'Agents Online',
            'dashboard.activeSessions': 'Active Sessions',
            'dashboard.pendingRequests': 'Pending Requests',
            'dashboard.approved': 'Approved',
            'dashboard.recordings': 'Recordings',
            'dashboard.policies': 'Policies',
            'dashboard.totalAgents': 'Total Agents',
            'dashboard.recentRequests': 'Recent Requests',
            'dashboard.recentSessions': 'Recent Sessions',
            'dashboard.agentStatus': 'Agent Status',
            'dashboard.lastSeen': 'Last Seen',
            'dashboard.noRequests': 'No requests',
            'dashboard.noSessions': 'No sessions',
            'common.target': 'Target',
            'common.user': 'User',
            'common.status': 'Status',
            'common.date': 'Date',
            'common.protocol': 'Protocol',
            'common.started': 'Started',
            'nav.dashboard': 'Dashboard',
            'nav.targets': 'Targets',
            'nav.policies': 'Policies',
            'nav.roles': 'Roles',
            'nav.agents': 'Agents',
            'nav.sessions': 'Sessions',
            'nav.recordings': 'Recordings',
            'nav.requests': 'Requests',
            'nav.approvals': 'Approvals',
            'nav.approverPanel': 'Approver Panel',
            'theme.toggle': 'Dark Theme',
            'lang.toggle': 'RU'
        }
    };

    function getTheme() {
        return localStorage.getItem(THEME_KEY) || 'light';
    }

    function setTheme(theme) {
        localStorage.setItem(THEME_KEY, theme);
        document.documentElement.setAttribute('data-bs-theme', theme);
        var btn = document.getElementById('theme-toggle');
        if (btn) {
            btn.textContent = theme === 'dark' ? '☀️' : '🌙';
            btn.title = theme === 'dark' ? 'Switch to light' : 'Switch to dark';
        }
    }

    function getLang() {
        return localStorage.getItem(LANG_KEY) || 'ru';
    }

    function setLang(lang) {
        localStorage.setItem(LANG_KEY, lang);
        var dict = translations[lang] || translations['ru'];
        document.querySelectorAll('[data-i18n]').forEach(function (el) {
            var key = el.getAttribute('data-i18n');
            if (dict[key]) el.textContent = dict[key];
        });
        var btn = document.getElementById('lang-toggle');
        if (btn) btn.textContent = dict['lang.toggle'] || lang.toUpperCase();
    }

    document.addEventListener('DOMContentLoaded', function () {
        setTheme(getTheme());
        setLang(getLang());

        var themeBtn = document.getElementById('theme-toggle');
        if (themeBtn) {
            themeBtn.addEventListener('click', function () {
                setTheme(getTheme() === 'dark' ? 'light' : 'dark');
            });
        }

        var langBtn = document.getElementById('lang-toggle');
        if (langBtn) {
            langBtn.addEventListener('click', function () {
                setLang(getLang() === 'ru' ? 'en' : 'ru');
            });
        }
    });

    setTheme(getTheme());
})();
