// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

// Menu de ações "⋯": usa posicionamento "fixed" para o menu não ficar
// cortado dentro de .table-responsive / DataTables. Corre antes do handler
// do Bootstrap (fase de captura) para criar o dropdown com esta configuração.
document.addEventListener('click', function (ev) {
    var btn = ev.target.closest && ev.target.closest('.btn-menu-acoes');
    if (btn && window.bootstrap) {
        bootstrap.Dropdown.getOrCreateInstance(btn, { popperConfig: { strategy: 'fixed' } });
    }
}, true);
