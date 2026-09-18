(function () {
  function applyFilters(root) {
    root.querySelectorAll('table').forEach(function (table) {
      if (typeof window.refreshTable === 'function') {
        window.refreshTable(table, { resetPage: true });
      }
    });
  }

  window.applyEstateFilters = applyFilters;

  function withServerQuery(url, server) {
    if (!server) return url;
    var join = url.indexOf('?') >= 0 ? '&' : '?';
    return url + join + 'server=' + encodeURIComponent(server);
  }

  var tabMap = {
    summary: '#tab-summary',
    findings: '#tab-findings',
    status: '#tab-status',
    databases: '#tab-databases',
    backups: '#tab-backups',
    volumes: '#tab-volumes',
    services: '#tab-services',
    waits: '#tab-waits',
    sysadmins: '#tab-sysadmins',
    jobs: '#tab-jobs',
    configure: '#tab-configure'
  };

  function activateTab(root, tabKey) {
    if (!tabKey) return false;
    var target = tabMap[String(tabKey).toLowerCase()];
    if (!target) return false;

    var btn = root.querySelector('.estate-tabs [data-bs-target="' + target + '"]');
    var pane = root.querySelector(target);
    if (!btn || !pane) return false;

    root.querySelectorAll('.estate-tabs .nav-link').forEach(function (el) {
      el.classList.remove('active');
      el.setAttribute('aria-selected', 'false');
    });
    root.querySelectorAll('.estate-tab-content > .tab-pane').forEach(function (el) {
      el.classList.remove('show', 'active');
    });

    btn.classList.add('active');
    btn.setAttribute('aria-selected', 'true');
    pane.classList.add('show', 'active');

    try {
      if (typeof bootstrap !== 'undefined' && bootstrap.Tab) {
        if (typeof bootstrap.Tab.getOrCreateInstance === 'function') {
          bootstrap.Tab.getOrCreateInstance(btn).show();
        } else {
          new bootstrap.Tab(btn).show();
        }
      }
    } catch (e) { /* classes already applied */ }

    return true;
  }

  function activateSeverity(root, severity) {
    if (!severity) return;
    var chip = root.querySelector('.sev-chip[data-severity="' + severity + '"]');
    if (!chip) {
      // Case-insensitive fallback
      root.querySelectorAll('.sev-chip[data-severity]').forEach(function (c) {
        if ((c.getAttribute('data-severity') || '').toLowerCase() === String(severity).toLowerCase()) {
          chip = c;
        }
      });
    }
    if (!chip) return;
    root.querySelectorAll('.sev-chip').forEach(function (c) { c.classList.remove('active'); });
    chip.classList.add('active');
  }

  function applyDeepLink(root, tabParam, severityParam) {
    if (tabParam) activateTab(root, tabParam);
    if (severityParam) activateSeverity(root, severityParam);
    applyFilters(root);
  }

  document.querySelectorAll('[data-estate-report]').forEach(function (root) {
    var serverSelect = root.querySelector('.server-filter');
    var runSelect = root.querySelector('.assessment-filter');
    var goBtn = root.querySelector('.filter-go-btn') || document.getElementById('assessmentGoBtn');
    var initialRun = runSelect ? runSelect.value : '';

    var params = new URLSearchParams(window.location.search);
    var serverParam = params.get('server');
    var tabParam = params.get('tab');
    var severityParam = params.get('severity');

    if (serverSelect && serverParam) {
      serverSelect.value = serverParam;
    }

    function runSearch() {
      var server = serverSelect ? serverSelect.value : '';
      if (runSelect && runSelect.value && runSelect.value !== initialRun) {
        var url = withServerQuery(runSelect.value, server);
        if (tabParam) {
          url += (url.indexOf('?') >= 0 ? '&' : '?') + 'tab=' + encodeURIComponent(tabParam);
          if (severityParam) url += '&severity=' + encodeURIComponent(severityParam);
        }
        window.location.href = url;
        return;
      }
      applyFilters(root);
    }

    if (goBtn) {
      goBtn.addEventListener('click', runSearch);
    }

    if (serverSelect) {
      serverSelect.addEventListener('keydown', function (e) {
        if (e.key === 'Enter') {
          e.preventDefault();
          runSearch();
        }
      });
    }

    if (runSelect) {
      runSelect.addEventListener('keydown', function (e) {
        if (e.key === 'Enter') {
          e.preventDefault();
          runSearch();
        }
      });
    }

    root.querySelectorAll('.sev-chip').forEach(function (chip) {
      chip.addEventListener('click', function () {
        root.querySelectorAll('.sev-chip').forEach(function (c) { c.classList.remove('active'); });
        chip.classList.add('active');
        applyFilters(root);
      });
    });

    root.querySelectorAll('.sev-jump').forEach(function (card) {
      card.addEventListener('click', function () {
        var sev = card.getAttribute('data-severity');
        activateTab(root, 'findings');
        activateSeverity(root, sev);
        applyFilters(root);
      });
    });

    root.querySelectorAll('[data-bs-toggle="tab"]').forEach(function (tab) {
      tab.addEventListener('shown.bs.tab', function () { applyFilters(root); });
    });

    // Deep-link: ?tab=findings&severity=Critical
    applyDeepLink(root, tabParam, severityParam);
  });
})();
