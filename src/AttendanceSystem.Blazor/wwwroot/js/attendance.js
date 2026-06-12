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

window.renderAttendanceChart = (canvasId, labels, present, late, absent) => {
  const canvas = document.getElementById(canvasId);
  if (!canvas || !window.Chart) return;
  if (canvas._chart) canvas._chart.destroy();
  canvas._chart = new Chart(canvas, {
    type: 'bar',
    data: {
      labels,
      datasets: [
        { label: 'Ирсэн', data: present, backgroundColor: '#10B981', borderRadius: 8 },
        { label: 'Хоцорсон', data: late, backgroundColor: '#F59E0B', borderRadius: 8 },
        { label: 'Ирээгүй', data: absent, backgroundColor: '#EF4444', borderRadius: 8 }
      ]
    },
    options: {
      responsive: true,
      maintainAspectRatio: false,
      scales: { x: { stacked: true, grid: { display: false } }, y: { stacked: true, beginAtZero: true } },
      plugins: { legend: { position: 'bottom' } }
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
