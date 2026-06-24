window.getLocation = () => new Promise((resolve, reject) => {
  if (!navigator.geolocation) return reject("GPS дэмжигдэхгүй");
  navigator.geolocation.getCurrentPosition(
    pos => resolve({ lat: pos.coords.latitude, lng: pos.coords.longitude, accuracy: pos.coords.accuracy }),
    err => reject(err.message),
    { enableHighAccuracy: true, timeout: 10000, maximumAge: 30000 }
  );
});

window.watchLocation = (dotnetRef) => {
  if (!navigator.geolocation) {
    dotnetRef.invokeMethodAsync('OnLocationError', 'GPS дэмжигдэхгүй');
    return -1;
  }

  return navigator.geolocation.watchPosition(
    pos => dotnetRef.invokeMethodAsync('OnLocationUpdate', pos.coords.latitude, pos.coords.longitude),
    err => dotnetRef.invokeMethodAsync('OnLocationError', err.message),
    { enableHighAccuracy: true, timeout: 10000 }
  );
};

window.clearWatch = (watchId) => {
  if (watchId >= 0 && navigator.geolocation) navigator.geolocation.clearWatch(watchId);
};

window.getDistance = (lat1, lng1, lat2, lng2) => {
  const R = 6371000;
  const dLat = (lat2 - lat1) * Math.PI / 180;
  const dLng = (lng2 - lng1) * Math.PI / 180;
  const a = Math.sin(dLat / 2) ** 2 +
    Math.cos(lat1 * Math.PI / 180) * Math.cos(lat2 * Math.PI / 180) *
    Math.sin(dLng / 2) ** 2;
  return Math.round(R * 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a)));
};

window.addRipple = (el, event) => {
  if (!el) return;
  const rect = el.getBoundingClientRect();
  const ripple = document.createElement('span');
  const size = Math.max(rect.width, rect.height);
  ripple.className = 'ripple';
  ripple.style.width = ripple.style.height = `${size}px`;
  ripple.style.left = `${(event?.clientX ?? rect.left + rect.width / 2) - rect.left - size / 2}px`;
  ripple.style.top = `${(event?.clientY ?? rect.top + rect.height / 2) - rect.top - size / 2}px`;
  el.appendChild(ripple);
  window.setTimeout(() => ripple.remove(), 650);
};

window.countUp = (elementId, target, duration = 1000) => {
  const el = document.getElementById(elementId);
  if (!el) return;
  const numericTarget = Number(String(target).replace(/[^0-9.-]/g, ''));
  if (!Number.isFinite(numericTarget)) {
    el.textContent = target;
    return;
  }
  const start = performance.now();
  const suffix = String(target).replace(/[0-9.-]/g, '');
  const step = (now) => {
    const progress = Math.min((now - start) / duration, 1);
    el.textContent = `${Math.round(numericTarget * progress)}${suffix}`;
    if (progress < 1) requestAnimationFrame(step);
  };
  requestAnimationFrame(step);
};

window.startClock = (elementId) => {
  const tick = () => {
    const el = document.getElementById(elementId);
    if (!el) return;
    el.textContent = new Date().toLocaleTimeString('mn-MN', { hour: '2-digit', minute: '2-digit', second: '2-digit' });
  };
  tick();
  return setInterval(tick, 1000);
};

window.stopClock = (clockId) => clearInterval(clockId);

window.renderAttendanceChart = (canvasId, labels, present, late, absent, onLeave) => {
  const canvas = document.getElementById(canvasId);
  if (!canvas || !window.Chart) return;
  if (canvas._chart) canvas._chart.destroy();

  const style = getComputedStyle(document.documentElement);
  const muted  = style.getPropertyValue('--clr-text-muted').trim()  || '#64748B';
  const border  = style.getPropertyValue('--clr-border').trim()      || '#E2E8F0';
  const surface = style.getPropertyValue('--clr-surface').trim()     || '#fff';

  const makeGrad = (ctx, color) => {
    const g = ctx.createLinearGradient(0, 0, 0, canvas.height);
    g.addColorStop(0, color + '33');
    g.addColorStop(1, color + '00');
    return g;
  };

  const ctx2d = canvas.getContext('2d');

  const datasets = [
    {
      label: 'Ирсэн',
      data: present,
      borderColor: '#16A34A',
      backgroundColor: makeGrad(ctx2d, '#16A34A'),
      fill: true,
      tension: 0.4,
      borderWidth: 2.5,
      pointRadius: 4,
      pointHoverRadius: 6,
      pointBackgroundColor: '#16A34A',
      pointBorderColor: surface,
      pointBorderWidth: 2
    },
    {
      label: 'Хоцорсон',
      data: late,
      borderColor: '#D97706',
      backgroundColor: makeGrad(ctx2d, '#D97706'),
      fill: true,
      tension: 0.4,
      borderWidth: 2,
      pointRadius: 3,
      pointHoverRadius: 5,
      pointBackgroundColor: '#D97706',
      pointBorderColor: surface,
      pointBorderWidth: 2
    },
    {
      label: 'Ирээгүй',
      data: absent,
      borderColor: '#DC2626',
      backgroundColor: makeGrad(ctx2d, '#DC2626'),
      fill: true,
      tension: 0.4,
      borderWidth: 2,
      pointRadius: 3,
      pointHoverRadius: 5,
      pointBackgroundColor: '#DC2626',
      pointBorderColor: surface,
      pointBorderWidth: 2
    }
  ];

  if (Array.isArray(onLeave)) {
    datasets.push({
      label: 'Чөлөөтэй',
      data: onLeave,
      borderColor: '#7C3AED',
      backgroundColor: makeGrad(ctx2d, '#7C3AED'),
      fill: true,
      tension: 0.4,
      borderWidth: 2,
      pointRadius: 3,
      pointHoverRadius: 5,
      pointBackgroundColor: '#7C3AED',
      pointBorderColor: surface,
      pointBorderWidth: 2
    });
  }

  canvas._chart = new Chart(canvas, {
    type: 'line',
    data: {
      labels,
      datasets
    },
    options: {
      responsive: true,
      maintainAspectRatio: false,
      interaction: { mode: 'index', intersect: false },
      scales: {
        x: {
          grid: { display: false },
          border: { display: false },
          ticks: { color: muted, font: { size: 11 } }
        },
        y: {
          beginAtZero: true,
          grid: { color: border, drawBorder: false },
          border: { display: false },
          ticks: { color: muted, font: { size: 11 }, stepSize: 1, precision: 0 }
        }
      },
      plugins: {
        legend: {
          position: 'bottom',
          labels: {
            color: muted,
            boxWidth: 10,
            boxHeight: 10,
            borderRadius: 5,
            useBorderRadius: true,
            padding: 16,
            font: { size: 12 }
          }
        },
        tooltip: {
          backgroundColor: surface,
          titleColor: '#0F172A',
          bodyColor: muted,
          borderColor: border,
          borderWidth: 1,
          padding: 10,
          boxPadding: 4
        }
      }
    }
  });
};

window.renderMonthlyChart = (canvasId, labels, attendanceRates, workingHours, overtime, lateMinutes) => {
  const canvas = document.getElementById(canvasId);
  if (!canvas || !window.Chart) return;
  if (canvas._chart) canvas._chart.destroy();
  canvas._chart = new Chart(canvas, {
    data: {
      labels,
      datasets: [
        {
          type: 'line',
          label: 'Attendance %',
          data: attendanceRates,
          yAxisID: 'y1',
          borderColor: '#3B82F6',
          backgroundColor: 'rgba(59,130,246,0.1)',
          tension: 0.4,
          fill: true
        },
        {
          type: 'bar',
          label: 'Working Hours',
          data: workingHours,
          yAxisID: 'y2',
          backgroundColor: '#10B981',
          borderRadius: 6
        },
        {
          type: 'bar',
          label: 'Overtime',
          data: overtime,
          yAxisID: 'y2',
          backgroundColor: '#F59E0B',
          borderRadius: 6
        },
        {
          type: 'bar',
          label: 'Late Minutes',
          data: lateMinutes,
          yAxisID: 'y2',
          backgroundColor: '#EF4444',
          borderRadius: 6
        }
      ]
    },
    options: {
      responsive: true,
      maintainAspectRatio: false,
      scales: {
        x: { grid: { display: false } },
        y1: {
          type: 'linear',
          position: 'left',
          beginAtZero: true,
          ticks: {
            callback: function(value) { return value + '%'; }
          }
        },
        y2: {
          type: 'linear',
          position: 'right',
          beginAtZero: true,
          grid: { display: false }
        }
      },
      plugins: { legend: { position: 'bottom' } }
    }
  });
};