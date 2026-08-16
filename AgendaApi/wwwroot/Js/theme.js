// =============================================
// THEME MANAGER - Modo escuro global
// =============================================

(function() {
    // Carregar tema salvo
    function loadTheme() {
        const saved = localStorage.getItem('theme');
        if (saved === 'dark') {
            document.documentElement.setAttribute('data-theme', 'dark');
            updateButtonIcon('dark');
        } else {
            document.documentElement.setAttribute('data-theme', 'light');
            updateButtonIcon('light');
        }
    }

    // Atualizar ícone do botão (se existir)
    function updateButtonIcon(theme) {
        const btn = document.getElementById('globalThemeToggle');
        if (!btn) return;
        const icon = btn.querySelector('.icon');
        const label = btn.querySelector('.label');
        if (theme === 'dark') {
            icon.textContent = '☀️';
            label.textContent = 'Claro';
        } else {
            icon.textContent = '🌙';
            label.textContent = 'Escuro';
        }
    }

    // Alternar tema
    window.toggleTheme = function() {
        const html = document.documentElement;
        const current = html.getAttribute('data-theme');
        const newTheme = current === 'dark' ? 'light' : 'dark';
        html.setAttribute('data-theme', newTheme);
        localStorage.setItem('theme', newTheme);
        updateButtonIcon(newTheme);
    };

    // Criar botão flutuante (se não existir)
    function createFloatingButton() {
        if (document.getElementById('globalThemeToggle')) return;

        const btn = document.createElement('button');
        btn.id = 'globalThemeToggle';
        btn.className = 'theme-float';
        btn.innerHTML = `<span class="icon">🌙</span> <span class="label">Escuro</span>`;
        btn.title = 'Alternar tema';
        btn.onclick = toggleTheme;

        // Estilo do botão flutuante
        const style = document.createElement('style');
        style.textContent = `
            .theme-float {
                position: fixed;
                bottom: 24px;
                right: 24px;
                z-index: 9999;
                background: var(--card-bg, #ffffff);
                border: 1px solid var(--border-color, #e5e7eb);
                border-radius: 40px;
                padding: 10px 20px;
                display: flex;
                align-items: center;
                gap: 8px;
                cursor: pointer;
                font-size: 14px;
                font-weight: 500;
                color: var(--text-color, #1e293b);
                box-shadow: 0 4px 16px rgba(0,0,0,0.15);
                transition: all 0.3s ease;
                font-family: 'Segoe UI', Roboto, sans-serif;
            }
            .theme-float:hover {
                transform: scale(1.05);
                box-shadow: 0 6px 24px rgba(0,0,0,0.25);
            }
            .theme-float .icon {
                font-size: 20px;
            }
            .theme-float .label {
                font-size: 14px;
            }
            @media (max-width: 600px) {
                .theme-float {
                    bottom: 16px;
                    right: 16px;
                    padding: 8px 14px;
                }
                .theme-float .label {
                    display: none;
                }
            }
        `;
        document.head.appendChild(style);
        document.body.appendChild(btn);

        // Atualizar ícone conforme tema atual
        const current = document.documentElement.getAttribute('data-theme');
        updateButtonIcon(current || 'light');
    }

    // Inicializar
    document.addEventListener('DOMContentLoaded', function() {
        loadTheme();
        createFloatingButton();
    });

    // Se o DOM já estiver carregado, executar imediatamente
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', function() {
            loadTheme();
            createFloatingButton();
        });
    } else {
        loadTheme();
        createFloatingButton();
    }
})();