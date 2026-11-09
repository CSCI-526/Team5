// Team5 analytics receiver. Bind this script to a Google Sheet, then deploy it as a web app.
// Each event name gets its own tab. Columns are appended when new keys appear and never reordered.

function doPost(e) {
  var events = [];
  try {
    var body = JSON.parse(e && e.postData && e.postData.contents ? e.postData.contents : '{}');
    events = Array.isArray(body.events) ? body.events : [];
  } catch (error) {
    return ContentService.createTextOutput('bad json');
  }
  if (!events.length) return ContentService.createTextOutput('ok');

  var lock = LockService.getScriptLock();
  lock.waitLock(20000);
  try {
    var groups = {};
    events.forEach(function (event) {
      if (!event || typeof event !== 'object') return;
      var name = String(event.event || 'unknown').replace(/[^A-Za-z0-9_]/g, '_').slice(0, 90) || 'unknown';
      (groups[name] = groups[name] || []).push(event);
    });
    var book = SpreadsheetApp.getActiveSpreadsheet();
    Object.keys(groups).forEach(function (name) {
      appendRows(book.getSheetByName(name) || book.insertSheet(name), groups[name]);
    });
    SpreadsheetApp.flush();
  } finally {
    lock.releaseLock();
  }
  return ContentService.createTextOutput('ok');
}

function doGet() {
  return ContentService.createTextOutput('ok');
}

function appendRows(sheet, rows) {
  var headers = sheet.getLastColumn() > 0 ? sheet.getRange(1, 1, 1, sheet.getLastColumn()).getValues()[0] : [];
  var known = {};
  headers.forEach(function (header, index) { known[header] = index; });
  rows.forEach(function (row) {
    Object.keys(row).forEach(function (key) {
      if (known.hasOwnProperty(key)) return;
      known[key] = headers.length;
      headers.push(key);
    });
  });
  sheet.getRange(1, 1, 1, headers.length).setValues([headers]);
  sheet.setFrozenRows(1);
  var values = rows.map(function (row) {
    return headers.map(function (header) {
      var value = row[header];
      return value === undefined || value === null ? '' : value;
    });
  });
  sheet.getRange(sheet.getLastRow() + 1, 1, values.length, headers.length).setValues(values);
}
