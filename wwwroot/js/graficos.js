// Gráficos da Plataforma DEI (Chart.js).
// Cada <canvas class="js-grafico" data-grafico='{"type":..., "data":..., "options":...}'> é desenhado
// automaticamente. Valores monetários (CVE) são formatados em pt-PT.
(function () {
    if (typeof Chart === 'undefined') return;

    var paleta = ['#3874ff', '#25b003', '#e5780b', '#ec1f00', '#0097eb', '#a43fbf', '#6e7891', '#c4a000'];
    var numero = new Intl.NumberFormat('pt-PT', { maximumFractionDigits: 0 });

    function cor(i, alpha) {
        var hex = paleta[i % paleta.length];
        if (alpha === undefined) return hex;
        var r = parseInt(hex.substr(1, 2), 16), g = parseInt(hex.substr(3, 2), 16), b = parseInt(hex.substr(5, 2), 16);
        return 'rgba(' + r + ',' + g + ',' + b + ',' + alpha + ')';
    }

    function desenhar(canvas) {
        var config;
        try { config = JSON.parse(canvas.getAttribute('data-grafico')); } catch (e) { return; }
        var moeda = canvas.getAttribute('data-moeda') === 'cve';
        var porFatia = config.type === 'doughnut' || config.type === 'pie';

        (config.data.datasets || []).forEach(function (ds, i) {
            if (ds.backgroundColor) return;
            if (porFatia) {
                ds.backgroundColor = ds.data.map(function (_, j) { return cor(j); });
            } else if (ds.type === 'line' || config.type === 'line') {
                ds.borderColor = cor(i);
                ds.backgroundColor = cor(i, 0.15);
                ds.tension = 0.25;
            } else {
                ds.backgroundColor = cor(i, 0.8);
                ds.borderRadius = 4;
            }
        });

        var fmt = function (v) { return moeda ? numero.format(v) + ' CVE' : numero.format(v); };
        config.options = Object.assign({
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                legend: { position: porFatia ? 'right' : 'top' },
                tooltip: {
                    callbacks: {
                        label: function (ctx) {
                            var v = ctx.parsed && typeof ctx.parsed === 'object' ? (config.options && config.options.indexAxis === 'y' ? ctx.parsed.x : ctx.parsed.y) : ctx.parsed;
                            return (ctx.dataset.label ? ctx.dataset.label + ': ' : (ctx.label ? ctx.label + ': ' : '')) + fmt(v);
                        }
                    }
                }
            }
        }, config.options || {});

        if (!porFatia) {
            var eixoValor = config.options.indexAxis === 'y' ? 'x' : 'y';
            config.options.scales = config.options.scales || {};
            config.options.scales[eixoValor] = Object.assign({ beginAtZero: true, ticks: { callback: function (v) { return numero.format(v); } } }, config.options.scales[eixoValor] || {});
        }

        new Chart(canvas, config);
    }

    document.addEventListener('DOMContentLoaded', function () {
        document.querySelectorAll('canvas.js-grafico').forEach(desenhar);
    });
})();
