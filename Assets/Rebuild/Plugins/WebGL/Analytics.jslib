// Team5 analytics transport for WebGL. Unity pushes one JSON event at a time;
// the page batches them and flushes on a timer, at 20 events, and when the tab hides or closes.
mergeInto(LibraryManager.library, {
  AnalyticsInit: function (urlPtr, intervalMs) {
    var url = UTF8ToString(urlPtr);
    if (!url || window.team5Analytics) return;
    var maxBytes = 60000;
    var state = { url: url, buffer: [], context: null, startedAt: Date.now(), exited: false };

    function size(text) {
      try { return new Blob([text]).size; } catch (e) { return text.length * 3; }
    }

    function post(parts) {
      var body = '{"events":[' + parts.join(',') + ']}';
      try {
        if (navigator.sendBeacon && navigator.sendBeacon(state.url, new Blob([body], { type: 'text/plain;charset=utf-8' }))) return;
      } catch (e) { }
      try {
        fetch(state.url, { method: 'POST', body: body, mode: 'no-cors', keepalive: true,
          headers: { 'Content-Type': 'text/plain;charset=utf-8' } });
      } catch (e) { }
    }

    // Splits the buffer so each request body stays under the beacon size limit.
    state.flush = function () {
      if (!state.buffer.length) return;
      var events = state.buffer;
      state.buffer = [];
      var parts = [], bytes = 16;
      for (var i = 0; i < events.length; i++) {
        var eventBytes = size(events[i]) + 1;
        if (parts.length && bytes + eventBytes > maxBytes) { post(parts); parts = []; bytes = 16; }
        parts.push(events[i]);
        bytes += eventBytes;
      }
      if (parts.length) post(parts);
    };

    state.exit = function () {
      if (state.exited) return;
      state.exited = true;
      if (state.context) {
        var exit = {};
        for (var key in state.context) exit[key] = state.context[key];
        exit.event = 'page_exit';
        exit.ts = new Date().toISOString();
        exit.seq = (Number(state.context.seq) || 0) + 1;
        exit.seconds_in_session = Math.round((Date.now() - state.startedAt) / 100) / 10;
        state.buffer.push(JSON.stringify(exit));
      }
      state.flush();
    };

    setInterval(state.flush, Math.max(1000, intervalMs | 0));
    window.addEventListener('pagehide', state.exit);
    window.addEventListener('pageshow', function (e) { if (e.persisted) state.exited = false; });
    document.addEventListener('visibilitychange', function () {
      if (document.visibilityState === 'hidden') state.flush();
    });
    window.team5Analytics = state;
  },

  AnalyticsEnqueue: function (jsonPtr) {
    var state = window.team5Analytics;
    if (!state) return;
    state.buffer.push(UTF8ToString(jsonPtr));
    if (state.buffer.length >= 20) state.flush();
  },

  AnalyticsSetContext: function (jsonPtr) {
    var state = window.team5Analytics;
    if (!state) return;
    try { state.context = JSON.parse(UTF8ToString(jsonPtr)); } catch (e) { }
  }
});
