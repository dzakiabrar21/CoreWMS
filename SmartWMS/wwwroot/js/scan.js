(function () {
  var ctx;
  function tone(freq, start, dur, type) {
    try {
      ctx = ctx || new (window.AudioContext || window.webkitAudioContext)();
      if (ctx.state === 'suspended') ctx.resume();
      var o = ctx.createOscillator(), g = ctx.createGain();
      o.type = type || 'sine';
      o.frequency.value = freq;
      g.gain.setValueAtTime(0.25, ctx.currentTime + start);
      g.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + start + dur);
      o.connect(g);
      g.connect(ctx.destination);
      o.start(ctx.currentTime + start);
      o.stop(ctx.currentTime + start + dur);
    } catch (e) {
      console.warn("Audio playback not allowed:", e);
    }
  }

  function beepOk() {
    tone(880, 0, 0.12);
    tone(1320, 0.13, 0.18);
  }

  function beepErr() {
    tone(180, 0, 0.35, 'sawtooth');
    tone(140, 0.38, 0.35, 'sawtooth');
  }

  function render(lines) {
    lines.forEach(function (l) {
      var $r = $('#progress [data-item="' + l.itemId + '"]');
      var pct = l.requested ? Math.round((l.loaded * 100) / l.requested) : 0;
      $r.find('.cnt').text(l.loaded + ' / ' + l.requested);
      $r.find('.progress-bar')
        .css('width', pct + '%')
        .toggleClass('bg-success', pct >= 100)
        .toggleClass('bg-primary', pct < 100);
    });
  }

  function scan() {
    var code = $.trim($('#barcode').val());
    if (!code) return;

    var qtyVal = $('#qty').val() || 1;

    $.post('/Dispatch/Scan', { id: DO_ID, barcode: code, qty: qtyVal }, function (r) {
      var $i = $('#indicator').removeClass('idle success danger bg-secondary bg-success bg-danger');

      if (r.success) {
        $i.addClass('success').html(`
          <i class="bi bi-check-circle-fill fs-1 mb-1"></i>
          <div>${r.message}</div>
          <small class="fw-normal fs-6 text-light mt-1">Verifikasi Barcode Berhasil</small>
        `);
        beepOk();
      } else {
        $i.addClass('danger').html(`
          <i class="bi bi-x-octagon-fill fs-1 mb-1"></i>
          <div>${r.message}</div>
          <small class="fw-normal fs-6 text-light mt-1">Poka-Yoke: Verifikasi Gagal / Ditolak</small>
        `);
        beepErr();
      }

      if (r.lines && r.lines.length) render(r.lines);

      if (r.completed) {
        $('#barcode, #btnScan, #qty').prop('disabled', true);
        $i.html(`
          <i class="bi bi-patch-check-fill fs-1 mb-1"></i>
          <div>SELURUH ITEM BERHASIL DIMUAT (100%)</div>
          <small class="fw-normal fs-6 text-light mt-1">DO Selesai &amp; Stok Gudang Berhasil Dipotong</small>
        `);
        setTimeout(function () {
          location.reload();
        }, 2200);
      }
    }).fail(function () {
      $('#indicator').removeClass('idle success danger').addClass('danger').html(`
        <i class="bi bi-wifi-off fs-1 mb-1"></i>
        <div>GAGAL MENGHUBUNGI SERVER</div>
        <small class="fw-normal fs-6 text-light mt-1">Periksa koneksi jaringan Anda</small>
      `);
      beepErr();
    }).always(function () {
      $('#barcode').val('').focus();
    });
  }

  $('#barcode').on('keydown', function (e) {
    if (e.key === 'Enter') {
      e.preventDefault();
      scan();
    }
  });

  $('#btnScan').on('click', scan);
})();
