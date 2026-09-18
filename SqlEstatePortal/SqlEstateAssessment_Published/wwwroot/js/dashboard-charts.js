(function () {
  var el = document.getElementById('dashboard-charts');
  if (!el || typeof Chart === 'undefined') return;

  var data;
  try {
    data = JSON.parse(el.textContent || '{}');
  } catch (e) {
    return;
  }

  var root = document.getElementById('dashboardRoot');
  var reportUrl = root ? (root.getAttribute('data-report-url') || '') : '';
  var selectedServer = root ? (root.getAttribute('data-server') || '') : '';

  Chart.defaults.font.family = '"Segoe UI", Calibri, sans-serif';
  Chart.defaults.color = '#5c6b7a';
  Chart.defaults.plugins.legend.labels.boxWidth = 12;
  Chart.defaults.plugins.legend.position = 'bottom';

  var palette = ['#1f4e79', '#2b6cb0', '#0e7490', '#2f855a', '#c05621', '#b42318', '#4a5568', '#2c5282', '#276749', '#9a4a00'];
  var severityColors = {
    Critical: '#b42318',
    High: '#c05621',
    Medium: '#b7791f',
    Low: '#2f855a',
    Info: '#2b6cb0',
    Unknown: '#718096'
  };

  function labels(items) {
    return (items || []).map(function (x) { return x.label; });
  }

  function values(items) {
    return (items || []).map(function (x) { return Number(x.value); });
  }

  function colorsFor(items) {
    return (items || []).map(function (x, i) {
      return severityColors[x.label] || palette[i % palette.length];
    });
  }

  function buildUrl(query) {
    if (!reportUrl) return null;
    var params = new URLSearchParams();
    Object.keys(query || {}).forEach(function (key) {
      var value = query[key];
      if (value != null && value !== '') params.set(key, value);
    });
    if (selectedServer && !params.has('server')) params.set('server', selectedServer);
    var qs = params.toString();
    return qs ? (reportUrl + (reportUrl.indexOf('?') >= 0 ? '&' : '?') + qs) : reportUrl;
  }

  function go(query) {
    var url = buildUrl(query);
    if (url) window.location.href = url;
  }

  function pointerCursor(chart) {
    var canvas = chart.canvas;
    canvas.style.cursor = 'pointer';
    canvas.addEventListener('mousemove', function (evt) {
      var points = chart.getElementsAtEventForMode(evt, 'nearest', { intersect: true }, true);
      canvas.style.cursor = points.length ? 'pointer' : 'default';
    });
  }

  function doughnut(id, items, clickHandler) {
    var canvas = document.getElementById(id);
    if (!canvas) return;
    var chart = new Chart(canvas, {
      type: 'doughnut',
      data: {
        labels: labels(items),
        datasets: [{
          data: values(items),
          backgroundColor: colorsFor(items),
          borderWidth: 0
        }]
      },
      options: {
        maintainAspectRatio: false,
        cutout: '58%',
        plugins: {
          legend: {
            position: 'bottom',
            onClick: function (e, legendItem) {
              if (!clickHandler) return;
              clickHandler(legendItem.text);
            }
          }
        },
        onClick: function (evt, elements) {
          if (!clickHandler || !elements.length) return;
          var label = chart.data.labels[elements[0].index];
          clickHandler(label);
        }
      }
    });
    if (clickHandler) pointerCursor(chart);
  }

  function bar(id, items, opts) {
    var canvas = document.getElementById(id);
    if (!canvas) return;
    var horizontal = !opts || opts.horizontal !== false;
    var clickHandler = opts && opts.onClickLabel;
    var helpMap = (opts && opts.helpMap) || {};
    var chart = new Chart(canvas, {
      type: 'bar',
      data: {
        labels: labels(items),
        datasets: [{
          data: values(items),
          backgroundColor: opts && opts.color ? opts.color : '#2b6cb0',
          borderRadius: 4,
          maxBarThickness: 22
        }]
      },
      options: {
        indexAxis: horizontal ? 'y' : 'x',
        maintainAspectRatio: false,
        plugins: {
          legend: { display: false },
          tooltip: {
            callbacks: {
              afterBody: function (tooltipItems) {
                if (!tooltipItems.length) return '';
                var label = tooltipItems[0].label;
                var help = helpMap[label];
                return help ? ['', help] : '';
              }
            }
          }
        },
        scales: {
          x: { grid: { display: !horizontal }, beginAtZero: true },
          y: {
            grid: { display: horizontal },
            ticks: {
              autoSkip: false,
              callback: function (value) {
                var label = this.getLabelForValue(value);
                return label;
              }
            }
          }
        },
        onClick: function (evt, elements) {
          if (!clickHandler || !elements.length) return;
          var label = chart.data.labels[elements[0].index];
          clickHandler(label);
        }
      },
      plugins: [{
        id: 'axisLabelHelp',
        afterEvent: function (chartInstance, args) {
          if (!horizontal || !Object.keys(helpMap).length) return;
          var event = args.event;
          if (!event || event.type !== 'mousemove') return;
          var yScale = chartInstance.scales.y;
          if (!yScale) return;
          var tip = canvas._axisHelpTip;
          if (!tip) {
            tip = document.createElement('div');
            tip.className = 'chart-axis-tip';
            tip.setAttribute('role', 'tooltip');
            canvas.parentNode.appendChild(tip);
            canvas._axisHelpTip = tip;
            if (getComputedStyle(canvas.parentNode).position === 'static') {
              canvas.parentNode.style.position = 'relative';
            }
          }
          var found = null;
          for (var i = 0; i < chartInstance.data.labels.length; i++) {
            var y = yScale.getPixelForTick(i);
            if (Math.abs(event.y - y) <= 12 && event.x <= yScale.right + 8) {
              found = chartInstance.data.labels[i];
              break;
            }
          }
          var help = found ? helpMap[found] : null;
          if (!help) {
            tip.style.display = 'none';
            return;
          }
          tip.textContent = found + ': ' + help;
          tip.style.display = 'block';
          var rect = canvas.parentNode.getBoundingClientRect();
          var left = Math.min(event.x + 14, rect.width - 280);
          var top = Math.max(8, event.y - 10);
          tip.style.left = left + 'px';
          tip.style.top = top + 'px';
        }
      }]
    });
    if (clickHandler) pointerCursor(chart);
  }

  doughnut('chartSeverity', data.findingsBySeverity, function (label) {
    go({ tab: 'findings', severity: label });
  });
  doughnut('chartSupport', data.supportStatus, function () {
    go({ tab: 'status' });
  });
  doughnut('chartRecovery', data.recoveryModels, function () {
    go({ tab: 'databases' });
  });
  doughnut('chartJobs', data.jobStatus, function () {
    go({ tab: 'jobs' });
  });
  doughnut('chartServices', data.serviceStatus, function () {
    go({ tab: 'services' });
  });
  doughnut('chartEditions', data.editions, function () {
    go({ tab: 'status' });
  });
  var areaHelp = {
    SLA: 'Backup and DBCC CHECKDB service levels — missing or overdue full/log backups and integrity checks against expected schedules.',
    Standards: 'Instance and database best-practice configuration (for example optimize for ad hoc workloads, CLR, page verify).',
    Cost: 'Cost and efficiency opportunities (for example backup compression not enabled as the instance default).',
    Security: 'Security posture — privileged logins, surface area, and risky settings.',
    Performance: 'Performance health — memory limits, waits, and related runtime risks.',
    Licensing: 'Edition, core count, and licensing alignment risks.',
    Supportability: 'Microsoft support lifecycle — SQL versions approaching or past end of support.',
    Status: 'Instance reachability and estate status signals.'
  };

  bar('chartArea', data.findingsByArea, {
    helpMap: areaHelp,
    onClickLabel: function () { go({ tab: 'findings' }); }
  });
  bar('chartServer', data.findingsByServer, {
    onClickLabel: function (label) { go({ tab: 'findings', server: label }); }
  });
  bar('chartDatabases', data.topDatabasesMb, {
    onClickLabel: function () { go({ tab: 'databases' }); }
  });
  bar('chartVolumes', data.volumeFreePct, {
    color: '#0e7490',
    onClickLabel: function () { go({ tab: 'volumes' }); }
  });
  bar('chartWaits', data.topWaits, {
    color: '#1f4e79',
    onClickLabel: function () { go({ tab: 'waits' }); }
  });

  var history = data.runHistory || [];
  var historyCanvas = document.getElementById('chartHistory');
  if (historyCanvas) {
    var historyChart = new Chart(historyCanvas, {
      type: 'bar',
      data: {
        labels: history.map(function (x) { return x.label; }),
        datasets: [
          { label: 'Critical', data: history.map(function (x) { return x.critical; }), backgroundColor: '#b42318', stack: 's' },
          { label: 'High', data: history.map(function (x) { return x.high; }), backgroundColor: '#c05621', stack: 's' },
          { label: 'Medium', data: history.map(function (x) { return x.medium; }), backgroundColor: '#b7791f', stack: 's' },
          { label: 'Low', data: history.map(function (x) { return x.low; }), backgroundColor: '#2f855a', stack: 's' }
        ]
      },
      options: {
        maintainAspectRatio: false,
        plugins: {
          legend: {
            position: 'bottom',
            onClick: function (e, legendItem) {
              go({ tab: 'findings', severity: legendItem.text });
            }
          }
        },
        scales: {
          x: { stacked: true, grid: { display: false } },
          y: { stacked: true, beginAtZero: true }
        },
        onClick: function (evt, elements) {
          if (!elements.length) return;
          var ds = historyChart.data.datasets[elements[0].datasetIndex];
          if (ds && ds.label) go({ tab: 'findings', severity: ds.label });
        }
      }
    });
    pointerCursor(historyChart);
  }
})();
