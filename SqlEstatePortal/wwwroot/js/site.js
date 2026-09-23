(function () {
  var STORAGE_KEY = 'sqlEstate.sidebarHidden';
  var toggle = document.getElementById('sidebarToggle');
  var menuBtn = document.getElementById('sidebarMenuBtn');
  var hideBtn = document.getElementById('sidebarHideBtn');
  var backdrop = document.getElementById('sidebarBackdrop');
  var narrowMq = window.matchMedia('(max-width: 1200px)');
  var mobileMq = window.matchMedia('(max-width: 900px)');

  function isMobileNav() {
    return mobileMq.matches;
  }

  function isNarrowDesktop() {
    return !isMobileNav() && narrowMq.matches;
  }

  function isCollapsedUi() {
    if (isMobileNav()) return !document.body.classList.contains('sidebar-open');
    return document.body.classList.contains('sidebar-hidden');
  }

  function syncToggleLabel() {
    var collapsed = isCollapsedUi();
    var label = isMobileNav()
      ? (collapsed ? 'Show menu' : 'Close menu')
      : (collapsed ? 'Expand menu' : 'Collapse menu');
    if (toggle) {
      toggle.setAttribute('aria-label', label);
      toggle.setAttribute('title', label);
    }
    if (menuBtn) {
      menuBtn.setAttribute('aria-label', label);
      menuBtn.setAttribute('title', label);
    }
    if (hideBtn) {
      hideBtn.setAttribute('title', collapsed ? 'Expand menu' : 'Collapse menu');
    }
  }

  function closeMobileMenu() {
    if (document.body.classList.contains('sidebar-open')) {
      document.body.classList.remove('sidebar-open');
      syncToggleLabel();
    }
  }

  function setDesktopCollapsed(collapsed) {
    document.body.classList.toggle('sidebar-hidden', collapsed);
    if (isNarrowDesktop()) {
      document.body.classList.toggle('sidebar-force-open', !collapsed);
    } else {
      document.body.classList.remove('sidebar-force-open');
    }
    try { localStorage.setItem(STORAGE_KEY, collapsed ? '1' : '0'); } catch (e) { /* ignore */ }
    syncToggleLabel();
  }

  function applyResponsiveSidebar() {
    if (isMobileNav()) {
      document.body.classList.remove('sidebar-force-open');
      syncToggleLabel();
      return;
    }

    if (isNarrowDesktop()) {
      // Auto-collapse to icons so grids keep room; burger can expand.
      if (!document.body.classList.contains('sidebar-force-open')) {
        document.body.classList.add('sidebar-hidden');
      }
    } else {
      document.body.classList.remove('sidebar-force-open');
      try {
        document.body.classList.toggle('sidebar-hidden', localStorage.getItem(STORAGE_KEY) === '1');
      } catch (e) {
        document.body.classList.remove('sidebar-hidden');
      }
    }
    syncToggleLabel();
  }

  function toggleMenu() {
    if (isMobileNav()) {
      document.body.classList.toggle('sidebar-open');
      syncToggleLabel();
      return;
    }
    setDesktopCollapsed(!document.body.classList.contains('sidebar-hidden'));
  }

  applyResponsiveSidebar();

  if (toggle) toggle.addEventListener('click', toggleMenu);
  if (menuBtn) menuBtn.addEventListener('click', toggleMenu);
  if (hideBtn) hideBtn.addEventListener('click', function () {
    if (isMobileNav()) {
      closeMobileMenu();
    } else {
      setDesktopCollapsed(!document.body.classList.contains('sidebar-hidden'));
    }
  });

  if (backdrop) {
    backdrop.addEventListener('click', closeMobileMenu);
  }

  // Close drawer on mobile when clicking any navigation link
  document.querySelectorAll('.sidebar .nav-item').forEach(function (link) {
    link.addEventListener('click', function () {
      if (isMobileNav()) {
        closeMobileMenu();
      }
    });
  });

  // Close drawer on Escape key
  document.addEventListener('keydown', function (e) {
    if (e.key === 'Escape' && isMobileNav() && document.body.classList.contains('sidebar-open')) {
      closeMobileMenu();
    }
  });

  function onViewportChange() {
    applyResponsiveSidebar();
  }
  if (narrowMq.addEventListener) {
    narrowMq.addEventListener('change', onViewportChange);
    mobileMq.addEventListener('change', onViewportChange);
  } else {
    window.addEventListener('resize', onViewportChange);
  }

  /* ---- Searchable selects (all views) ---- */
  function enhanceSearchableSelect(select) {
    if (!select || select.dataset.searchable === '1') return;
    if (select.multiple || (select.size && select.size > 1)) return;
    if (select.classList.contains('table-page-size')) return;
    if (select.hasAttribute('data-no-search')) return;

    select.dataset.searchable = '1';
    select.classList.add('ss-native');

    var wrap = document.createElement('div');
    wrap.className = 'ss-wrap' + (select.classList.contains('form-select-sm') ? ' ss-sm' : '');
    if (select.classList.contains('assessment-filter')) wrap.classList.add('ss-wide');
    if (select.classList.contains('server-filter')) wrap.classList.add('ss-mid');

    select.parentNode.insertBefore(wrap, select);
    wrap.appendChild(select);

    var trigger = document.createElement('button');
    trigger.type = 'button';
    trigger.className = 'ss-trigger';
    trigger.setAttribute('aria-haspopup', 'listbox');
    trigger.setAttribute('aria-expanded', 'false');

    var panel = document.createElement('div');
    panel.className = 'ss-panel';
    panel.hidden = true;

    var search = document.createElement('input');
    search.type = 'search';
    search.className = 'ss-search form-control form-control-sm';
    search.placeholder = 'Search…';
    search.autocomplete = 'off';
    search.setAttribute('aria-label', 'Search options');

    var list = document.createElement('ul');
    list.className = 'ss-list';
    list.setAttribute('role', 'listbox');

    panel.appendChild(search);
    panel.appendChild(list);
    wrap.appendChild(trigger);
    wrap.appendChild(panel);

    var activeIndex = -1;

    function optionText(opt) {
      return (opt.textContent || '').replace(/\s+/g, ' ').trim();
    }

    function selectedLabel() {
      var opt = select.options[select.selectedIndex];
      return opt ? optionText(opt) : 'Select…';
    }

    function syncTrigger() {
      trigger.textContent = selectedLabel();
      trigger.classList.toggle('ss-placeholder', !select.value);
    }

    function closePanel() {
      if (panel.hidden) return;
      panel.hidden = true;
      trigger.setAttribute('aria-expanded', 'false');
      wrap.classList.remove('ss-open');
      activeIndex = -1;
    }

    function placePanel() {
      var rect = trigger.getBoundingClientRect();
      var width = Math.max(rect.width, 220);
      var left = Math.min(rect.left, window.innerWidth - width - 8);
      if (left < 8) left = 8;
      var top = rect.bottom + 4;
      var maxH = Math.min(280, window.innerHeight - top - 12);
      if (maxH < 140 && rect.top > 160) {
        top = Math.max(8, rect.top - 4 - Math.min(280, rect.top - 8));
        maxH = Math.min(280, rect.top - 12);
      }
      panel.style.position = 'fixed';
      panel.style.left = left + 'px';
      panel.style.top = top + 'px';
      panel.style.width = width + 'px';
      panel.style.maxHeight = maxH + 'px';
      list.style.maxHeight = Math.max(80, maxH - 48) + 'px';
    }

    function renderList(query) {
      var q = (query || '').toLowerCase().trim();
      list.innerHTML = '';
      activeIndex = -1;
      var visible = 0;
      Array.prototype.forEach.call(select.options, function (opt, idx) {
        var label = optionText(opt);
        if (q && label.toLowerCase().indexOf(q) === -1) return;
        var li = document.createElement('li');
        li.className = 'ss-option';
        li.setAttribute('role', 'option');
        li.dataset.index = String(idx);
        li.textContent = label || '(blank)';
        if (opt.disabled) {
          li.classList.add('ss-disabled');
          li.setAttribute('aria-disabled', 'true');
        }
        if (opt.selected) {
          li.classList.add('ss-selected');
          li.setAttribute('aria-selected', 'true');
        }
        list.appendChild(li);
        visible++;
      });
      if (!visible) {
        var empty = document.createElement('li');
        empty.className = 'ss-empty';
        empty.textContent = 'No matches';
        list.appendChild(empty);
      }
    }

    function openPanel() {
      if (select.disabled) return;
      renderList('');
      search.value = '';
      panel.hidden = false;
      trigger.setAttribute('aria-expanded', 'true');
      wrap.classList.add('ss-open');
      placePanel();
      setTimeout(function () { search.focus(); }, 0);
    }

    function pickIndex(idx) {
      var opt = select.options[idx];
      if (!opt || opt.disabled) return;
      select.selectedIndex = idx;
      syncTrigger();
      closePanel();
      select.dispatchEvent(new Event('change', { bubbles: true }));
      select.dispatchEvent(new Event('input', { bubbles: true }));
    }

    function moveActive(delta) {
      var items = list.querySelectorAll('.ss-option:not(.ss-disabled)');
      if (!items.length) return;
      activeIndex = (activeIndex + delta + items.length) % items.length;
      items.forEach(function (el, i) {
        el.classList.toggle('ss-active', i === activeIndex);
        if (i === activeIndex) el.scrollIntoView({ block: 'nearest' });
      });
    }

    trigger.addEventListener('click', function (e) {
      e.preventDefault();
      if (panel.hidden) openPanel();
      else closePanel();
    });

    list.addEventListener('click', function (e) {
      var li = e.target.closest('.ss-option');
      if (!li || li.classList.contains('ss-disabled')) return;
      pickIndex(parseInt(li.dataset.index, 10));
    });

    search.addEventListener('input', function () {
      renderList(search.value);
    });

    search.addEventListener('keydown', function (e) {
      if (e.key === 'Escape') {
        e.preventDefault();
        closePanel();
        trigger.focus();
      } else if (e.key === 'ArrowDown') {
        e.preventDefault();
        moveActive(1);
      } else if (e.key === 'ArrowUp') {
        e.preventDefault();
        moveActive(-1);
      } else if (e.key === 'Enter') {
        e.preventDefault();
        var items = list.querySelectorAll('.ss-option:not(.ss-disabled)');
        if (activeIndex >= 0 && items[activeIndex]) {
          pickIndex(parseInt(items[activeIndex].dataset.index, 10));
        } else if (items.length === 1) {
          pickIndex(parseInt(items[0].dataset.index, 10));
        }
      }
    });

    select.addEventListener('change', syncTrigger);

    document.addEventListener('mousedown', function (e) {
      if (!wrap.contains(e.target) && !panel.contains(e.target)) closePanel();
    });

    window.addEventListener('resize', function () {
      if (!panel.hidden) placePanel();
    });
    window.addEventListener('scroll', function () {
      if (!panel.hidden) placePanel();
    }, true);

    syncTrigger();
  }

  function initSearchableSelects(root) {
    (root || document).querySelectorAll('select.form-select').forEach(enhanceSearchableSelect);
  }

  window.initSearchableSelects = initSearchableSelects;
  initSearchableSelects(document);

  var PAGE_SIZES = [10, 25, 50, 100];
  // Keep in step with the selected option in Views/Shared/_TableToolbar.cshtml.
  var PAGE_SIZE_DEFAULT = 100;

  function rowText(row) {
    return (row.textContent || '').replace(/\s+/g, ' ').toLowerCase();
  }

  function getState(table) {
    if (!table._tableUi) table._tableUi = { page: 1, pageSize: PAGE_SIZE_DEFAULT, sortCol: null, sortDir: 'asc' };
    return table._tableUi;
  }

  function estateContext(table) {
    var root = table.closest('[data-estate-report]');
    var chipRoot = root || table.closest('.panel') || document;
    var sevBtn = chipRoot.querySelector('.sev-chip.active');
    var matchBtn = chipRoot.querySelector('.sev-chip.active[data-match-filter], .qa-tile.active[data-match-filter]')
      || document.querySelector('.qa-tile.active[data-match-filter]');
    return {
      server: root ? ((root.querySelector('.server-filter') || {}).value || '') : '',
      severity: (sevBtn && sevBtn.hasAttribute('data-severity')) ? (sevBtn.getAttribute('data-severity') || '') : '',
      match: matchBtn ? (matchBtn.getAttribute('data-match-filter') || '') : '',
      diffStatus: sevBtn ? (sevBtn.getAttribute('data-diff-filter') || '') : ''
    };
  }

  function rowMatches(row, query, colQueries, ctx) {
    if (query && rowText(row).indexOf(query) === -1) return false;
    if (ctx.server) {
      var rowServer = row.getAttribute('data-server');
      if (rowServer && rowServer !== ctx.server) return false;
    }
    if (ctx.severity) {
      var rowSev = row.getAttribute('data-severity');
      if (rowSev && rowSev !== ctx.severity) return false;
    }
    if (ctx.match) {
      var rowMatch = row.getAttribute('data-match');
      if (rowMatch !== ctx.match) return false;
    }
    if (ctx.diffStatus) {
      var rowDiff = row.getAttribute('data-diff-status');
      if (rowDiff !== ctx.diffStatus) return false;
    }
    if (colQueries && colQueries.length) {
      for (var i = 0; i < colQueries.length; i++) {
        var colQuery = colQueries[i];
        if (!colQuery) continue;
        var cell = row.cells[i];
        var text = cell ? (cell.textContent || '').replace(/\s+/g, ' ').toLowerCase() : '';
        if (text.indexOf(colQuery) === -1) return false;
      }
    }
    return true;
  }

  function columnQueries(table) {
    var queries = [];
    (table._columnInputs || []).forEach(function (input, i) {
      queries[i] = input ? input.value.trim().toLowerCase() : '';
    });
    return queries;
  }

  // ---------------------------------------------------------------------
  // CSV export
  //
  // Every grid built by attachTable gets an "Export to Excel" button. The
  // export is a UTF-8 CSV with a BOM so Excel picks the encoding up without
  // being told. It contains every row matching the current search, column
  // filters and estate context (server, severity, QA match tile) - not just
  // the page on screen - because it reuses rowMatches, the same predicate
  // refreshTable uses to decide what is on the grid.
  // ---------------------------------------------------------------------

  function csvEscape(value) {
    var text = String(value == null ? '' : value);
    if (/[",\r\n]/.test(text)) return '"' + text.replace(/"/g, '""') + '"';
    return text;
  }

  function cellExportText(cell) {
    if (!cell) return '';

    // A cell can contain UI that is not data - the findings grid puts its hover
    // history panel inside the Finding cell. Anything marked data-export="skip"
    // is removed from a clone before the text is read, so the CSV carries the
    // finding and not the tooltip that happens to sit in the same cell.
    if (cell.querySelector('[data-export="skip"]')) {
      var clone = cell.cloneNode(true);
      Array.prototype.forEach.call(
        clone.querySelectorAll('[data-export="skip"]'),
        function (el) { el.parentNode.removeChild(el); });
      return (clone.textContent || '').replace(/\s+/g, ' ').trim();
    }

    return (cell.textContent || '').replace(/\s+/g, ' ').trim();
  }

  // Columns worth exporting: everything except the UI-only ones (action
  // links, checkbox pickers, unlabelled spacers). A view can force a column
  // out with data-export="skip" on its header cell.
  function exportColumns(table) {
    var headRow = headerSortRow(table);
    if (!headRow) return null;
    var columns = [];
    Array.prototype.forEach.call(headRow.cells, function (th, index) {
      if ((th.getAttribute('data-export') || '') === 'skip') return;
      if (th.querySelector('input, select, textarea')) return;
      var label = (th.textContent || '').replace(/\s+/g, ' ').trim();
      if (!label) return;
      if (/^(actions?|options)$/i.test(label)) return;
      columns.push({ index: index, label: label });
    });
    return columns.length ? columns : null;
  }

  // A placeholder row ("No data", "Loading…") is one wide cell, not data.
  function isPlaceholderRow(row) {
    return row.cells.length === 1 && (row.cells[0].colSpan || 1) > 1;
  }

  function nearestHeadingText(table) {
    var node = table;
    while (node && node !== document.body) {
      var sib = node.previousElementSibling;
      while (sib) {
        if (/^H[1-4]$/.test(sib.tagName)) return sib.textContent;
        var inner = sib.querySelector ? sib.querySelector('h1, h2, h3, h4') : null;
        if (inner) return inner.textContent;
        sib = sib.previousElementSibling;
      }
      node = node.parentElement;
    }
    var h1 = document.querySelector('main h1') || document.querySelector('h1');
    return h1 ? h1.textContent : (document.title || 'export');
  }

  function exportFileName(table) {
    var explicit = (table.getAttribute('data-export-name') || '').trim();
    var base = explicit || nearestHeadingText(table) || 'export';
    base = base.replace(/\s+/g, ' ').trim().replace(/[\\/:*?"<>|]/g, '').slice(0, 80);
    if (!base) base = 'export';
    var now = new Date();
    var stamp = now.getFullYear() +
      ('0' + (now.getMonth() + 1)).slice(-2) +
      ('0' + now.getDate()).slice(-2);
    return base + ' - ' + stamp + '.csv';
  }

  function exportTableCsv(table) {
    if (!table || !table.tBodies.length) return;
    var columns = exportColumns(table);
    if (!columns) return;

    var query = table._searchInput ? table._searchInput.value.trim().toLowerCase() : '';
    var colQueries = columnQueries(table);
    var ctx = estateContext(table);

    var lines = [columns.map(function (c) { return csvEscape(c.label); }).join(',')];
    Array.prototype.forEach.call(table.querySelectorAll('tbody tr'), function (row) {
      if (isPlaceholderRow(row)) return;
      if (!rowMatches(row, query, colQueries, ctx)) return;
      lines.push(columns.map(function (c) {
        return csvEscape(cellExportText(row.cells[c.index]));
      }).join(','));
    });

    var blob = new Blob(['﻿' + lines.join('\r\n')], { type: 'text/csv;charset=utf-8;' });
    var url = URL.createObjectURL(blob);
    var a = document.createElement('a');
    a.href = url;
    a.download = exportFileName(table);
    document.body.appendChild(a);
    a.click();
    a.remove();
    URL.revokeObjectURL(url);
  }

  window.exportTableCsv = exportTableCsv;

  // Views declare their own toolbars in some places and let attachTable build
  // one in others. Normalising to a right-hand actions group keeps the search
  // box and the export button together under the toolbar's space-between.
  function toolbarActions(toolbar) {
    var actions = toolbar.querySelector(':scope > .table-toolbar-actions');
    if (actions) return actions;
    actions = document.createElement('div');
    actions.className = 'table-toolbar-actions';
    var search = toolbar.querySelector(':scope > .table-search-input');
    if (search) {
      toolbar.insertBefore(actions, search);
      actions.appendChild(search);
    } else {
      toolbar.appendChild(actions);
    }
    return actions;
  }

  function attachExportButton(table, toolbar) {
    if (!toolbar) return;
    if (table.hasAttribute('data-no-export')) return;
    if (!exportColumns(table)) return;
    if (toolbar.querySelector(':scope > .table-toolbar-actions > .table-export-btn')) return;

    var btn = document.createElement('button');
    btn.type = 'button';
    btn.className = 'btn btn-outline-secondary btn-sm table-export-btn';
    btn.textContent = 'Export to Excel';
    btn.title = 'Download every row matching the current filters as a CSV file that opens in Excel';
    btn.addEventListener('click', function () { exportTableCsv(table); });
    toolbarActions(toolbar).appendChild(btn);
  }

  function cellSortKey(cell) {
    var text = cell ? (cell.textContent || '').replace(/\s+/g, ' ').trim() : '';
    if (!text || text === '—' || text === '-') return { t: 0, n: 0, s: '' };

    var numeric = parseNumericCell(text);
    if (numeric != null) return { t: 1, n: numeric, s: text.toLowerCase() };

    if (/^\d{1,2}[\/\-]\d{1,2}[\/\-]\d{2,4}/.test(text) ||
        /^\d{4}[\/\-]\d{1,2}[\/\-]\d{1,2}/.test(text) ||
        /\d{1,2}:\d{2}/.test(text)) {
      var ms = Date.parse(text);
      if (!isNaN(ms)) return { t: 1, n: ms, s: text.toLowerCase() };
    }

    return { t: 2, n: 0, s: text.toLowerCase() };
  }

  function parseNumericCell(text) {
    var raw = String(text || '')
      .replace(/\u00a0/g, ' ')
      .replace(/[£$€¥]/g, '')
      .trim();
    if (!raw) return null;

    var neg = false;
    if (/^\(.*\)$/.test(raw)) {
      neg = true;
      raw = raw.slice(1, -1).trim();
    }

    // Keep digits, separators, sign, percent; drop other letters/symbols
    var cleaned = raw
      .replace(/\s/g, '')
      .replace(/,/g, '')
      .replace(/%$/, '');

    if (!/^-?\d+(\.\d+)?$/.test(cleaned)) return null;
    var n = parseFloat(cleaned);
    if (isNaN(n)) return null;
    return neg ? -n : n;
  }

  function compareSortKeys(a, b) {
    if (a.t === 1 && b.t === 1) {
      if (a.n < b.n) return -1;
      if (a.n > b.n) return 1;
      return 0;
    }
    if (a.t === 1 && b.t !== 1) return -1;
    if (b.t === 1 && a.t !== 1) return 1;
    if (a.s < b.s) return -1;
    if (a.s > b.s) return 1;
    return 0;
  }

  function applySort(table) {
    var state = getState(table);
    if (state.sortCol == null || !table.tBodies.length) return;
    var tbody = table.tBodies[0];
    var rows = Array.prototype.slice.call(tbody.rows);
    var col = state.sortCol;
    var dir = state.sortDir === 'desc' ? -1 : 1;
    rows.sort(function (ra, rb) {
      var ka = cellSortKey(ra.cells[col]);
      var kb = cellSortKey(rb.cells[col]);
      // Blanks always last (both asc and desc)
      if (ka.t === 0 && kb.t === 0) return 0;
      if (ka.t === 0) return 1;
      if (kb.t === 0) return -1;
      return compareSortKeys(ka, kb) * dir;
    });
    rows.forEach(function (row) { tbody.appendChild(row); });
  }

  function syncSortIndicators(table) {
    var state = getState(table);
    (table._sortHeaders || []).forEach(function (th) {
      var col = parseInt(th.getAttribute('data-sort-col'), 10);
      th.classList.remove('sort-asc', 'sort-desc');
      th.removeAttribute('aria-sort');
      if (col === state.sortCol) {
        th.classList.add(state.sortDir === 'desc' ? 'sort-desc' : 'sort-asc');
        th.setAttribute('aria-sort', state.sortDir === 'desc' ? 'descending' : 'ascending');
      }
    });
  }

  function headerSortRow(table) {
    var head = table.tHead;
    if (!head) return null;
    var preferred = head.querySelector('tr.column-head-row');
    if (preferred) return preferred;
    for (var i = 0; i < head.rows.length; i++) {
      if (!head.rows[i].classList.contains('column-filters')) return head.rows[i];
    }
    return null;
  }

  function bindColumnSort(table) {
    var row = headerSortRow(table);
    if (!row) return;
    table._sortHeaders = [];
    Array.prototype.forEach.call(row.cells, function (th, colIndex) {
      if (th.classList.contains('th-no-sort')) return;
      var label = (th.textContent || '').replace(/\s+/g, ' ').trim();
      if (!label) {
        th.classList.add('th-no-sort');
        return;
      }

      th.classList.add('th-sortable');
      th.setAttribute('data-sort-col', String(colIndex));
      th.setAttribute('scope', 'col');

      var btn = th.querySelector('.th-sort-btn');
      if (!btn) {
        btn = document.createElement('button');
        btn.type = 'button';
        btn.className = 'th-sort-btn';
        while (th.firstChild) btn.appendChild(th.firstChild);
        th.appendChild(btn);
      }

      if (btn.dataset.sortBound === '1') {
        table._sortHeaders.push(th);
        return;
      }
      btn.dataset.sortBound = '1';
      btn.addEventListener('click', function (e) {
        e.preventDefault();
        var state = getState(table);
        if (state.sortCol === colIndex) {
          state.sortDir = state.sortDir === 'asc' ? 'desc' : 'asc';
        } else {
          state.sortCol = colIndex;
          state.sortDir = 'asc';
        }
        refreshTable(table, { resetPage: true });
      });
      table._sortHeaders.push(th);
    });
  }

  function pageWindow(current, total) {
    if (total <= 7) {
      var all = [];
      for (var i = 1; i <= total; i++) all.push(i);
      return all;
    }
    var pages = [1];
    var start = Math.max(2, current - 1);
    var end = Math.min(total - 1, current + 1);
    if (current <= 3) {
      start = 2;
      end = 4;
    }
    if (current >= total - 2) {
      start = total - 3;
      end = total - 1;
    }
    if (start > 2) pages.push('…');
    for (var p = start; p <= end; p++) pages.push(p);
    if (end < total - 1) pages.push('…');
    pages.push(total);
    return pages;
  }

  function renderPager(pager, state, matched, pages) {
    pager.innerHTML = '';
    var info = document.createElement('span');
    info.className = 'pager-info';
    if (!matched) {
      info.textContent = '0 rows';
    } else {
      var start = (state.page - 1) * state.pageSize + 1;
      var end = Math.min(state.page * state.pageSize, matched);
      info.textContent = start + '–' + end + ' of ' + matched;
    }
    pager.appendChild(info);

    var buttons = document.createElement('div');
    buttons.className = 'pager-buttons';

    function addBtn(label, page, disabled, active) {
      var btn = document.createElement('button');
      btn.type = 'button';
      btn.className = 'btn btn-sm btn-outline-secondary' + (active ? ' active' : '');
      btn.textContent = label;
      btn.disabled = !!disabled;
      if (!disabled && page) btn.setAttribute('data-page', String(page));
      buttons.appendChild(btn);
    }

    addBtn('Prev', state.page - 1, state.page <= 1);
    pageWindow(state.page, pages).forEach(function (item) {
      if (item === '…') {
        var ellipsis = document.createElement('span');
        ellipsis.className = 'pager-ellipsis';
        ellipsis.textContent = '…';
        buttons.appendChild(ellipsis);
        return;
      }
      addBtn(String(item), item, false, item === state.page);
    });
    addBtn('Next', state.page + 1, state.page >= pages);
    pager.appendChild(buttons);
    pager.hidden = matched === 0;
  }

  function refreshTable(table, options) {
    if (!table || !table.tBodies.length) return;
    var state = getState(table);
    if (options && options.resetPage) state.page = 1;
    if (table._pageSizeSelect) {
      var chosen = parseInt(table._pageSizeSelect.value, 10);
      if (chosen) state.pageSize = chosen;
    }

    applySort(table);
    syncSortIndicators(table);

    var query = table._searchInput ? table._searchInput.value.trim().toLowerCase() : '';
    var colQueries = columnQueries(table);
    var ctx = estateContext(table);
    var rows = Array.prototype.slice.call(table.querySelectorAll('tbody tr'));
    var matched = rows.filter(function (row) { return rowMatches(row, query, colQueries, ctx); });
    var pages = Math.max(1, Math.ceil(matched.length / state.pageSize) || 1);
    if (state.page > pages) state.page = pages;

    var start = (state.page - 1) * state.pageSize;
    var end = start + state.pageSize;
    rows.forEach(function (row) {
      row.hidden = true;
      row.style.display = 'none';
    });
    matched.forEach(function (row, index) {
      var show = index >= start && index < end;
      row.hidden = !show;
      row.style.display = show ? '' : 'none';
    });

    if (table._pager) renderPager(table._pager, state, matched.length, pages);
    if (table._hint) table._hint.hidden = matched.length > 0 || rows.length === 0;
  }

  window.refreshTable = refreshTable;

  function bindColumnFilters(table) {
    table._columnInputs = [];
    var filterRow = table.querySelector('thead tr.column-filters');
    if (!filterRow) return;
    Array.prototype.forEach.call(filterRow.cells, function (cell, i) {
      var input = cell.querySelector('input.column-search');
      table._columnInputs[i] = input || null;
      if (!input || input.dataset.bound === '1') return;
      input.dataset.bound = '1';
      input.addEventListener('input', function () { refreshTable(table, { resetPage: true }); });
      input.addEventListener('keydown', function (e) {
        if (e.key === 'Enter') e.preventDefault();
      });
    });
  }

  function attachTable(table) {
    if (table.hasAttribute('data-no-table-ui')) return;
    if (!table.querySelector('tbody tr') || table.dataset.tableUi === '1') return;
    table.dataset.tableUi = '1';

    var toolbar = table.previousElementSibling;
    if (!toolbar || !toolbar.classList.contains('table-toolbar')) {
      toolbar = table.parentElement && table.parentElement.querySelector(':scope > .table-toolbar');
    }
    if (!toolbar || !toolbar.classList.contains('table-toolbar')) {
      toolbar = document.createElement('div');
      toolbar.className = 'table-toolbar';

      var sizeWrap = document.createElement('label');
      sizeWrap.className = 'table-page-size-label';
      sizeWrap.appendChild(document.createTextNode('Show '));
      var sizeSelect = document.createElement('select');
      sizeSelect.className = 'form-select form-select-sm table-page-size';
      PAGE_SIZES.forEach(function (n) {
        var opt = document.createElement('option');
        opt.value = String(n);
        opt.textContent = String(n);
        if (n === PAGE_SIZE_DEFAULT) opt.selected = true;
        sizeSelect.appendChild(opt);
      });
      sizeWrap.appendChild(sizeSelect);
      sizeWrap.appendChild(document.createTextNode(' entries'));

      var input = document.createElement('input');
      input.type = 'search';
      input.className = 'form-control table-search-input';
      input.placeholder = 'Search all columns...';
      input.setAttribute('aria-label', 'Search all columns');
      input.autocomplete = 'off';

      toolbar.appendChild(sizeWrap);
      toolbar.appendChild(input);
      table.parentNode.insertBefore(toolbar, table);
    }

    table._searchInput = toolbar.querySelector('.table-search-input');
    table._pageSizeSelect = toolbar.querySelector('.table-page-size');

    if (table._searchInput && table._searchInput.dataset.bound !== '1') {
      table._searchInput.dataset.bound = '1';
      table._searchInput.addEventListener('input', function () { refreshTable(table, { resetPage: true }); });
      table._searchInput.addEventListener('keydown', function (e) {
        if (e.key === 'Enter') e.preventDefault();
      });
    }
    if (table._pageSizeSelect && table._pageSizeSelect.dataset.bound !== '1') {
      table._pageSizeSelect.dataset.bound = '1';
      table._pageSizeSelect.addEventListener('change', function () { refreshTable(table, { resetPage: true }); });
    }

    bindColumnFilters(table);
    bindColumnSort(table);
    attachExportButton(table, toolbar);

    var pager = table.nextElementSibling;
    if (!pager || !pager.classList.contains('table-pager')) {
      pager = document.createElement('div');
      pager.className = 'table-pager';
      table.parentNode.insertBefore(pager, table.nextSibling);
    }
    table._pager = pager;

    var hint = pager.nextElementSibling;
    if (!hint || !hint.classList.contains('empty-hint')) {
      hint = document.createElement('p');
      hint.className = 'muted empty-hint';
      hint.hidden = true;
      hint.textContent = 'No matching rows.';
      table.parentNode.insertBefore(hint, pager.nextSibling);
    }
    table._hint = hint;

    if (!pager.dataset.bound) {
      pager.dataset.bound = '1';
      pager.addEventListener('click', function (e) {
        var btn = e.target.closest('button[data-page]');
        if (!btn) return;
        getState(table).page = parseInt(btn.getAttribute('data-page'), 10);
        refreshTable(table);
      });
    }

    refreshTable(table);
  }

  document.querySelectorAll('table').forEach(attachTable);

  document.querySelectorAll('.sev-chip[data-match-filter], .sev-chip[data-diff-filter], .qa-tile[data-match-filter]').forEach(function (chip) {
    if (chip.dataset.tableFilterBound === '1') return;
    chip.dataset.tableFilterBound = '1';
    chip.addEventListener('click', function () {
      var group = chip.closest('.severity-tabs') || chip.closest('.qa-summary-tiles') || chip.parentElement;
      if (group) {
        group.querySelectorAll('.sev-chip, .qa-tile').forEach(function (c) { c.classList.remove('active'); });
      }
      chip.classList.add('active');
      var scope = chip.closest('.tab-pane') || chip.closest('.panel') || document;
      var tables = scope.querySelectorAll('table');
      if (!tables.length) tables = document.querySelectorAll('#qaCompareTable');
      tables.forEach(function (table) {
        refreshTable(table, { resetPage: true });
      });
    });
  });

  // ---------------------------------------------------------------------
  // Move To - fills the shared dialog from the clicked row, and refuses to
  // submit without a comment. The server checks the comment too; this is only
  // so the user finds out before a round trip.
  // ---------------------------------------------------------------------
  function bindMoveFinding() {
    var modalEl = document.getElementById('moveFindingModal');
    if (!modalEl || modalEl.dataset.bound === '1') return;
    modalEl.dataset.bound = '1';

    var form = document.getElementById('moveFindingForm');
    var idField = document.getElementById('moveFindingId');
    var toField = document.getElementById('moveFindingTo');
    var commentField = document.getElementById('moveFindingComment');
    var submitBtn = document.getElementById('moveFindingSubmit');

    function set(id, value) {
      var el = document.getElementById(id);
      if (el) el.textContent = value || '';
    }

    document.addEventListener('click', function (e) {
      var btn = e.target.closest('.js-move-finding');
      if (!btn) return;

      idField.value = btn.getAttribute('data-finding-id') || '';
      set('moveFindingServer', btn.getAttribute('data-server'));
      set('moveFindingArea', btn.getAttribute('data-area'));
      set('moveFindingCurrent', btn.getAttribute('data-severity'));
      set('moveFindingText', btn.getAttribute('data-finding'));

      toField.value = '';
      commentField.value = '';
      commentField.setCustomValidity('');

      // The severity it already holds is not a move, so take it off the list
      // rather than letting the user pick it and get an error back.
      var current = (btn.getAttribute('data-severity') || '').toLowerCase();
      Array.prototype.forEach.call(toField.options, function (opt) {
        opt.hidden = opt.value !== '' && opt.value.toLowerCase() === current;
        opt.disabled = opt.hidden;
      });

      if (window.bootstrap && window.bootstrap.Modal) {
        window.bootstrap.Modal.getOrCreateInstance(modalEl).show();
      }
    });

    function validate() {
      var ok = commentField.value.trim().length > 0;
      commentField.setCustomValidity(ok ? '' : 'Enter a reason for the change.');
      return ok;
    }

    commentField.addEventListener('input', validate);

    form.addEventListener('submit', function (e) {
      // A comment of only spaces passes the browser's required check but is
      // rejected by the server, so trim before deciding.
      if (!validate() || !toField.value) {
        e.preventDefault();
        form.reportValidity();
        return;
      }
      submitBtn.disabled = true;
      submitBtn.textContent = 'Moving...';
    });
  }

  function bindAssessmentProgress() {
    var overlay = document.getElementById('assessmentProgressOverlay');
    var bar = document.getElementById('assessmentProgressBar');
    var pctLabel = document.getElementById('assessmentProgressPct');
    var titleEl = document.getElementById('assessmentProgressTitle');
    var msgEl = document.getElementById('assessmentProgressMsg');
    var track = overlay ? overlay.querySelector('.assessment-progress-track') : null;
    if (!overlay || !bar || !pctLabel) return;
    if (overlay.dataset.bound === '1') return;
    overlay.dataset.bound = '1';

    var timer = null;
    var current = 1;
    var startTimeEl = document.getElementById('assessmentStartTime');
    var elapsedTimeEl = document.getElementById('assessmentElapsedTime');
    var startTimestamp = null;
    var elapsedInterval = null;

    function formatDuration(ms) {
      var totalSec = Math.floor(Math.max(0, ms) / 1000);
      var mins = Math.floor(totalSec / 60);
      var secs = totalSec % 60;
      var pad = function (n) { return (n < 10 ? '0' : '') + n; };
      if (mins >= 60) {
        var hrs = Math.floor(mins / 60);
        mins = mins % 60;
        return pad(hrs) + ':' + pad(mins) + ':' + pad(secs);
      }
      return pad(mins) + ':' + pad(secs);
    }

    function startTiming() {
      stopTiming();
      startTimestamp = Date.now();
      var now = new Date(startTimestamp);
      var timeStr = now.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' });
      if (startTimeEl) startTimeEl.textContent = 'Started: ' + timeStr;
      if (elapsedTimeEl) elapsedTimeEl.textContent = 'Elapsed: 00:00';
      elapsedInterval = setInterval(function () {
        if (!startTimestamp) return;
        var elapsed = Date.now() - startTimestamp;
        if (elapsedTimeEl) elapsedTimeEl.textContent = 'Elapsed: ' + formatDuration(elapsed);
      }, 1000);
    }

    function stopTiming() {
      if (elapsedInterval) {
        clearInterval(elapsedInterval);
        elapsedInterval = null;
      }
    }

    // current is kept as a float and only rounded for display. With the
    // decelerating ticker below the steps become fractions of a percent, and
    // rounding the stored value would swallow them and freeze the bar.
    function setProgress(value) {
      current = Math.max(1, Math.min(100, value));
      var shown = Math.round(current);
      bar.style.width = shown + '%';
      pctLabel.textContent = shown + '%';
      if (track) track.setAttribute('aria-valuenow', String(shown));
    }

    function stopTicker() {
      if (timer) {
        clearInterval(timer);
        timer = null;
      }
    }

    // The bar used to climb in fixed steps and stop dead at 92, so on a run
    // longer than ~20s it sat frozen and looked hung. Instead each tick closes a
    // fraction of the gap to the ceiling: brisk at first, slower as it goes, and
    // never quite arriving. A long run keeps visibly counting instead of
    // freezing, and a short one still snaps to 100 when the work finishes.
    //
    // The ceiling is 99, not 100: the last percent belongs to the backend
    // actually finishing. Showing 100 while still waiting is the one thing more
    // annoying than stalling at 92.
    // Tuned against real run lengths: ~59% at 30s, ~83% at 60s, ~93% at 90s,
    // ~96% at two minutes, reaching 99 only after about two and a half. A
    // typical 60-90s assessment therefore sees the number moving the whole way
    // through rather than parked, which was the actual complaint - the old curve
    // hit its 92 ceiling after roughly 22 seconds and sat there.
    var PROGRESS_CEILING = 99;
    var PROGRESS_EASE = 0.015;    // fraction of the remaining gap per tick
    var PROGRESS_MIN_STEP = 0.02; // keeps the number climbing near the ceiling
    var PROGRESS_TICK_MS = 500;

    function startTicker() {
      stopTicker();
      setProgress(1);
      timer = setInterval(function () {
        var remaining = PROGRESS_CEILING - current;
        if (remaining <= 0) return;
        setProgress(current + Math.max(PROGRESS_MIN_STEP, remaining * PROGRESS_EASE));
      }, PROGRESS_TICK_MS);
    }

    function showOverlay(title, msg) {
      if (titleEl && title) titleEl.textContent = title;
      if (msgEl && msg) msgEl.textContent = msg;
      overlay.hidden = false;
      overlay.removeAttribute('hidden');
      overlay.classList.add('is-visible');
      overlay.setAttribute('aria-busy', 'true');
      document.body.style.overflow = 'hidden';
      startTicker();
      startTiming();
    }

    window.showEstateProgress = showOverlay;
    window.hideEstateProgress = hideOverlay;

    function hideOverlay() {
      stopTicker();
      stopTiming();
      startTimestamp = null;
      overlay.hidden = true;
      overlay.classList.remove('is-visible');
      overlay.setAttribute('aria-busy', 'false');
      document.body.style.overflow = '';
      setProgress(1);
    }

    function finishAndRedirect(url) {
      stopTicker();
      if (startTimestamp && elapsedTimeEl) {
        var finalElapsed = Date.now() - startTimestamp;
        elapsedTimeEl.textContent = 'Total: ' + formatDuration(finalElapsed);
      }
      stopTiming();
      setProgress(100);
      setTimeout(function () {
        window.location.href = url;
      }, 450);
    }

    function bindProgressForm(selector, defaults) {
      document.querySelectorAll(selector).forEach(function (form) {
        form.addEventListener('submit', function (e) {
          e.preventDefault();
          e.stopPropagation();
          var btn = form.querySelector('button[type="submit"]');
          if (btn) {
            btn.disabled = true;
            btn.dataset.originalText = btn.textContent;
            btn.textContent = defaults.busyText;
          }

          showOverlay(
            form.getAttribute('data-progress-title') || defaults.title,
            form.getAttribute('data-progress-msg') || defaults.msg
          );

          fetch(form.action, {
            method: 'POST',
            body: new FormData(form),
            headers: {
              'X-Requested-With': 'XMLHttpRequest',
              'Accept': 'application/json'
            },
            credentials: 'same-origin'
          })
            .then(function (res) {
              return res.json().then(function (data) {
                if (!res.ok) {
                  var msg = (data && data.message) || (defaults.failMessage + ' (' + res.status + ').');
                  throw new Error(msg);
                }
                return data;
              }).catch(function (err) {
                if (err && err.message && err.name !== 'SyntaxError') throw err;
                if (!res.ok) throw new Error(defaults.failMessage + ' (' + res.status + ').');
                throw err;
              });
            })
            .then(function (data) {
              var url = (data && data.redirectUrl) || form.getAttribute('data-fallback') || defaults.fallback;
              finishAndRedirect(url);
            })
            .catch(function (err) {
              hideOverlay();
              if (btn) {
                btn.disabled = false;
                btn.textContent = btn.dataset.originalText || defaults.idleText;
              }
              alert(err && err.message ? err.message : defaults.failMessage);
            });
        });
      });
    }

    function bindRunAssessmentWithServerPicker() {
      var modalEl = document.getElementById('runAssessmentModal');
      if (!modalEl || typeof bootstrap === 'undefined') {
        bindProgressForm('form.js-run-assessment', {
          title: 'Running assessment',
          msg: 'Collecting data from selected servers…',
          busyText: 'Running...',
          idleText: 'Run assessment',
          failMessage: 'Assessment request failed',
          fallback: '/Assessments'
        });
        return;
      }

      var modal = bootstrap.Modal.getOrCreateInstance(modalEl);
      var listEl = document.getElementById('runAssessmentList');
      var emptyEl = document.getElementById('runAssessmentEmpty');
      var countEl = document.getElementById('runAssessmentCount');
      var searchEl = document.getElementById('runAssessmentSearch');
      var confirmBtn = document.getElementById('runAssessmentConfirm');
      var pendingForm = null;

      function esc(v) {
        return String(v == null ? '' : v)
          .replace(/&/g, '&amp;')
          .replace(/</g, '&lt;')
          .replace(/>/g, '&gt;')
          .replace(/"/g, '&quot;');
      }

      function updateCount() {
        if (!countEl || !listEl) return;
        var n = listEl.querySelectorAll('input.run-assess-check:checked').length;
        countEl.textContent = n + ' selected';
        if (confirmBtn) confirmBtn.disabled = n === 0;
      }

      function applySearch() {
        var q = (searchEl && searchEl.value || '').trim().toLowerCase();
        listEl.querySelectorAll('.run-assess-row').forEach(function (row) {
          var name = (row.getAttribute('data-name') || '').toLowerCase();
          row.hidden = !!(q && name.indexOf(q) < 0);
        });
      }

      function renderServers(servers) {
        if (!listEl) return;
        if (!servers.length) {
          listEl.innerHTML = '';
          if (emptyEl) emptyEl.hidden = false;
          updateCount();
          return;
        }
        if (emptyEl) emptyEl.hidden = true;
        listEl.innerHTML = servers.map(function (s) {
          var name = s.name || '';
          var env = s.environment ? '<span class="muted">' + esc(s.environment) + '</span>' : '';
          return '<label class="run-assess-row" data-name="' + esc(name) + '">' +
            '<input type="checkbox" class="run-assess-check" value="' + esc(name) + '" checked />' +
            '<span class="run-assess-name"><strong>' + esc(name) + '</strong>' + env + '</span>' +
            '<span class="run-assess-status">Reachable</span>' +
            '</label>';
        }).join('');
        listEl.querySelectorAll('input.run-assess-check').forEach(function (cb) {
          cb.addEventListener('change', updateCount);
        });
        updateCount();
        applySearch();
      }

      function openPicker(form) {
        pendingForm = form;
        if (searchEl) searchEl.value = '';
        listEl.innerHTML = '<p class="muted mb-0">Loading Reachable servers…</p>';
        if (emptyEl) emptyEl.hidden = true;
        if (confirmBtn) confirmBtn.disabled = true;
        countEl.textContent = 'Loading…';
        modal.show();

        fetch('/Assessments/ReachableServers', {
          headers: { 'Accept': 'application/json' },
          credentials: 'same-origin'
        })
          .then(function (r) {
            if (!r.ok) throw new Error('Unable to load Reachable servers.');
            return r.json();
          })
          .then(function (data) {
            renderServers((data && data.servers) || []);
          })
          .catch(function (err) {
            listEl.innerHTML = '';
            if (emptyEl) {
              emptyEl.hidden = false;
              emptyEl.textContent = err && err.message ? err.message : 'Unable to load Reachable servers.';
            }
            updateCount();
          });
      }

      document.querySelectorAll('form.js-run-assessment').forEach(function (form) {
        form.addEventListener('submit', function (e) {
          e.preventDefault();
          e.stopPropagation();
          openPicker(form);
        });
      });

      document.getElementById('runAssessmentSelectAll')?.addEventListener('click', function () {
        listEl.querySelectorAll('.run-assess-row:not([hidden]) input.run-assess-check').forEach(function (cb) {
          cb.checked = true;
        });
        updateCount();
      });
      document.getElementById('runAssessmentClearAll')?.addEventListener('click', function () {
        listEl.querySelectorAll('input.run-assess-check').forEach(function (cb) {
          cb.checked = false;
        });
        updateCount();
      });
      if (searchEl) searchEl.addEventListener('input', applySearch);

      confirmBtn?.addEventListener('click', function () {
        if (!pendingForm) return;
        var selected = Array.prototype.map.call(
          listEl.querySelectorAll('input.run-assess-check:checked'),
          function (cb) { return cb.value; }
        ).filter(Boolean);
        if (!selected.length) {
          alert('Select at least one Reachable server.');
          return;
        }

        // Clear prior server inputs, then add selected.
        pendingForm.querySelectorAll('input[name="servers"]').forEach(function (el) { el.remove(); });
        selected.forEach(function (name) {
          var input = document.createElement('input');
          input.type = 'hidden';
          input.name = 'servers';
          input.value = name;
          pendingForm.appendChild(input);
        });

        modal.hide();

        var btn = pendingForm.querySelector('button[type="submit"]');
        if (btn) {
          btn.disabled = true;
          btn.dataset.originalText = btn.textContent;
          btn.textContent = 'Running...';
        }

        showOverlay(
          pendingForm.getAttribute('data-progress-title') || 'Running assessment',
          'Assessing ' + selected.length + ' selected server' + (selected.length === 1 ? '' : 's') + '…'
        );

        fetch(pendingForm.action, {
          method: 'POST',
          body: new FormData(pendingForm),
          headers: {
            'X-Requested-With': 'XMLHttpRequest',
            'Accept': 'application/json'
          },
          credentials: 'same-origin'
        })
          .then(function (res) {
            return res.json().then(function (data) {
              if (!res.ok) {
                throw new Error((data && data.message) || ('Assessment request failed (' + res.status + ').'));
              }
              return data;
            }).catch(function (err) {
              if (err && err.message && err.name !== 'SyntaxError') throw err;
              if (!res.ok) throw new Error('Assessment request failed (' + res.status + ').');
              throw err;
            });
          })
          .then(function (data) {
            var url = (data && data.redirectUrl) || pendingForm.getAttribute('data-fallback') || '/Assessments';
            finishAndRedirect(url);
          })
          .catch(function (err) {
            hideOverlay();
            if (btn) {
              btn.disabled = false;
              btn.textContent = btn.dataset.originalText || 'Run assessment';
            }
            alert(err && err.message ? err.message : 'Assessment request failed');
          });
      });
    }

    bindRunAssessmentWithServerPicker();

    bindProgressForm('form.js-check-server-status', {
      title: 'Checking server status',
      msg: 'Pinging servers and updating Reachable / UnReachable…',
      busyText: 'Checking...',
      idleText: 'Check Server Status',
      failMessage: 'Server status check failed',
      fallback: '/Assessments'
    });
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', function () {
      bindAssessmentProgress();
      bindMoveFinding();
    });
  } else {
    bindAssessmentProgress();
    bindMoveFinding();
  }
})();
