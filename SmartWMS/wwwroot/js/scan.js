(function () {
  var ctx;
  function tone(freq, start, dur, type) {
    ctx = ctx || new (window.AudioContext || window.webkitAudioContext)();
    var o = ctx.createOscillator(), g = ctx.createGain();
    o.type = type || 'sine'; o.frequency.value = freq;
    g.gain.setValueAtTime(0.25, ctx.currentTime + start);
    g.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + start + dur);
    o.connect(g); g.connect(ctx.destination);
    o.start(ctx.currentTime + start); o.stop(ctx.currentTime + start + dur);
  }
  function beepOk()  { tone(880, 0, 0.12); tone(1320, 0.13, 0.18); }
  function beepErr() { tone(160, 0, 0.35, 'sawtooth'); tone(140, 0.38, 0.35, 'sawtooth'); }

  function render(lines) {
    lines.forEach(function (l) {
      var $r = $('#progress [data-item="' + l.itemId + '"]');
      var pct = l.requested ? Math.round(l.loaded * 100 / l.requested) : 0;
      $r.find('.cnt').text(l.loaded + ' / ' + l.requested);
      $r.find('.progress-bar').css('width', pct + '%').toggleClass('bg-success', pct === 100);
    });
  }

  function scan() {
    var code = $.trim($('#barcode').val());
    if (!code) return;
    $.post('/Dispatch/Scan', { id: DO_ID, barcode: code, qty: $('#qty').val() || 1 }, function (r) {
      var $i = $('#indicator').removeClass('bg-secondary bg-success bg-danger')
        .addClass(r.success ? 'bg-success' : 'bg-danger').text(r.message);
      r.success ? beepOk() : beepErr();
      if (r.lines && r.lines.length) render(r.lines);
      if (r.completed) { $('#barcode, #btnScan').prop('disabled', true); setTimeout(function () { location.reload(); }, 2500); }
    }).fail(function () {
      $('#indicator').removeClass('bg-secondary bg-success').addClass('bg-danger').text('ERROR KONEKSI'); beepErr();
    }).always(function () { $('#barcode').val('').focus(); });
  }

  $('#barcode').on('keydown', function (e) { if (e.key === 'Enter') { e.preventDefault(); scan(); } });
  $('#btnScan').on('click', scan);
})();
