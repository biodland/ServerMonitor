/* ServerMonitor shared frontend helpers (no dependencies).
 *
 * Usage per page:
 *   <script src="/js/site.js"></script>
 *   <script>
 *     Site.loadChartJS().then(hasChart => { ... });
 *   </script>
 */
window.Site = (() => {
    'use strict';

    // ----- Formatting helpers (single source of truth for all pages) -----
    function fmtBytes(n) {
        if (n == null || isNaN(n)) return '--';
        const units = ['B', 'KiB', 'MiB', 'GiB', 'TiB', 'PiB'];
        let i = 0;
        while (n >= 1024 && i < units.length - 1) { n /= 1024; i++; }
        return (i === 0 ? n.toFixed(0) : n.toFixed(n < 10 ? 2 : 1)) + ' ' + units[i];
    }

    function fmtRate(n) {
        if (n == null || isNaN(n)) return '--';
        return fmtBytes(n) + '/s';
    }

    function fmtUptime(sec) {
        if (!sec) return '--';
        const d = Math.floor(sec / 86400);
        const h = Math.floor((sec % 86400) / 3600);
        const m = Math.floor((sec % 3600) / 60);
        if (d > 0) return `${d}d ${h}h ${m}m`;
        if (h > 0) return `${h}h ${m}m`;
        return `${m}m`;
    }

    function statusOf(pct, warn, bad) {
        if (pct >= bad) return 'bad';
        if (pct >= warn) return 'warning';
        return '';
    }

    function fmtTime(iso) {
        const d = new Date(iso);
        if (isNaN(d)) return '--';
        return d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
    }

    function fmtDateTime(iso) {
        const d = new Date(iso);
        if (isNaN(d)) return '--';
        const mm = String(d.getMonth() + 1).padStart(2, '0');
        const dd = String(d.getDate()).padStart(2, '0');
        const hh = String(d.getHours()).padStart(2, '0');
        const mi = String(d.getMinutes()).padStart(2, '0');
        return `${mm}-${dd} ${hh}:${mi}`;
    }

    // Short axis label: HH:MM for sub-day ranges, MM-DD HH:MM beyond.
    function fmtAxis(iso, longRange) {
        return longRange ? fmtDateTime(iso) : fmtTime(iso);
    }

    // Minimal HTML escaping for BMC/hardware-provided strings.
    function esc(s) {
        return String(s ?? '').replace(/[&<>"']/g, c => ({
            '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
        }[c]));
    }

    // ----- Chart.js loader: local copy first, CDNs as fallback -----
    const cdns = [
        '/js/chart.js',
        'https://cdn.jsdelivr.net/npm/chart.js@4.4.0/dist/chart.umd.min.js',
        'https://unpkg.com/chart.js@4.4.0/dist/chart.umd.min.js',
        'https://cdnjs.cloudflare.com/ajax/libs/Chart.js/4.4.0/chart.umd.min.js'
    ];

    function loadScript(src) {
        return new Promise((resolve, reject) => {
            const script = document.createElement('script');
            script.src = src;
            if (src.startsWith('http')) script.crossOrigin = 'anonymous';
            script.onload = () => resolve(true);
            script.onerror = () => reject(new Error('load failed: ' + src));
            document.head.appendChild(script);
        });
    }

    async function loadChartJS() {
        if (typeof Chart !== 'undefined') return true;
        for (const src of cdns) {
            try {
                await loadScript(src);
                if (typeof Chart !== 'undefined') {
                    console.log('Chart.js loaded from', src);
                    return true;
                }
            } catch (e) {
                console.warn(e.message, '- trying next...');
            }
        }
        console.error('All Chart.js sources failed');
        return false;
    }

    // ----- Chart factory: responsive mini/trend line charts -----
    // Canvas must sit inside a .chart-wrap (fixed height) container.
    function lineChart(canvas, opts = {}) {
        if (typeof Chart === 'undefined' || !canvas) return null;
        const color = opts.color || '#60a5fa';
        return new Chart(canvas, {
            type: 'line',
            data: {
                labels: [],
                datasets: (opts.series || [{ label: opts.label || '', color }]).map(s => ({
                    label: s.label || '',
                    data: [],
                    borderColor: s.color || color,
                    backgroundColor: (s.color || color) + '22',
                    fill: opts.fill !== false,
                    borderWidth: 1.5,
                    tension: 0.3
                }))
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                animation: { duration: opts.animate ? 300 : 0 },
                plugins: {
                    legend: { display: !!(opts.legend), labels: { color: '#888', boxWidth: 12, font: { size: 10 } } },
                    tooltip: {
                        // Axis labels are pre-formatted; tooltips echo them plus
                        // any per-chart label callbacks merged in via opts.tooltip.
                        callbacks: Object.assign(
                            { title: items => items[0].label },
                            opts.tooltip || {})
                    }
                },
                scales: {
                    x: {
                        ticks: { color: '#666', font: { size: 9 }, maxTicksLimit: 6, maxRotation: 0 },
                        grid: { display: false }
                    },
                    y: {
                        beginAtZero: opts.beginAtZero !== false,
                        suggestedMin: opts.min,
                        suggestedMax: opts.max,
                        grid: { color: 'rgba(255,255,255,0.05)' },
                        ticks: {
                            color: '#666',
                            font: { size: 9 },
                            maxTicksLimit: 5,
                            callback: opts.yFormat || (v => v)
                        }
                    }
                },
                elements: { point: { radius: 0 } }
            }
        });
    }

    function setChartData(chart, labels, ...seriesValues) {
        if (!chart) return;
        chart.data.labels = labels;
        seriesValues.forEach((vals, i) => {
            if (chart.data.datasets[i]) chart.data.datasets[i].data = vals;
        });
        chart.update('none');
    }

    function pushChartPoint(chart, label, ...values) {
        if (!chart) return;
        chart.data.labels.push(label);
        values.forEach((v, i) => {
            if (chart.data.datasets[i]) chart.data.datasets[i].data.push(v);
        });
        const max = chart.$maxPoints || 60;
        while (chart.data.labels.length > max) {
            chart.data.labels.shift();
            chart.data.datasets.forEach(ds => ds.shift());
        }
        chart.update('none');
    }

    return {
        fmtBytes, fmtRate, fmtUptime, statusOf,
        fmtTime, fmtDateTime, fmtAxis, esc,
        loadChartJS, lineChart, setChartData, pushChartPoint
    };
})();
