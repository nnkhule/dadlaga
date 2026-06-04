let checkedIn = false;
let checkedOut = false;
let checkInTime = null;
let checkOutTime = null;
let workInterval = null;

function updateClock() {
  const now = new Date();
  const h = String(now.getHours()).padStart(2, "0");
  const m = String(now.getMinutes()).padStart(2, "0");
  const s = String(now.getSeconds()).padStart(2, "0");

  const clock = document.getElementById("live-clock");
  if (clock) clock.textContent = `${h}:${m}:${s}`;
}

function buildCalendar() {
  const today = new Date();
  const year = today.getFullYear();
  const month = today.getMonth();
  const first = new Date(year, month, 1).getDay();
  const daysInMonth = new Date(year, month + 1, 0).getDate();
  const start = first === 0 ? 6 : first - 1;

  const grid = document.getElementById("cal-grid");
  if (!grid) return;

  let html = "";
  for (let i = 0; i < start; i++) {
    html += `<div class="mini-card" style="visibility:hidden"></div>`;
  }
  for (let d = 1; d <= daysInMonth; d++) {
    const isToday = d === today.getDate();
    html += `<div class="mini-card ${isToday ? "today-cell" : ""}"><strong>${d}</strong><span>${isToday ? "Өнөөдөр" : ""}</span></div>`;
  }
  grid.innerHTML = html;
}

function setButtonState(state) {
  const btn = document.getElementById("checkin-btn");
  const label = document.getElementById("btn-label");
  const hint = document.getElementById("btn-hint");

  if (!btn || !label || !hint) return;

  if (state === "in") {
    btn.className = "action-btn state-in";
    label.textContent = "ИРСЭН";
    hint.textContent = "Дарж ажилд ирсэн бүртгэлээ хийнэ үү";
  } else if (state === "out") {
    btn.className = "action-btn state-out";
    label.textContent = "ГАРСАН";
    hint.textContent = "Дарж ажлаас гарсан бүртгэлээ хийнэ үү";
  } else {
    btn.className = "action-btn state-done";
    label.textContent = "ДУУСЛАА";
    hint.textContent = "Өнөөдрийн бүртгэл дууслаа";
  }
}

function startWorkTimer() {
  stopWorkTimer();

  workInterval = setInterval(() => {
    if (!checkInTime || checkOutTime) return;

    const diff = Date.now() - checkInTime.getTime();
    const h = Math.floor(diff / 3600000);
    const m = Math.floor((diff % 3600000) / 60000);

    const timer = document.getElementById("work-timer");
    if (timer) timer.textContent = `${h}ц ${String(m).padStart(2, "0")}мин`;

    const pct = Math.min((diff / 3600000) / 8 * 100, 100);
    const bar = document.getElementById("work-bar");
    if (bar) {
      bar.style.width = `${pct}%`;
      bar.classList.toggle("overtime", pct >= 100);
    }
  }, 1000);
}

function stopWorkTimer() {
  if (workInterval) clearInterval(workInterval);
  workInterval = null;
}

function showOverlay(id, visible) {
  const el = document.getElementById(id);
  if (!el) return;
  el.classList.toggle("visible", !!visible);
}

function showSuccess(isOut, timeStr) {
  const title = document.getElementById("success-title");
  const time = document.getElementById("success-time");
  const text = document.getElementById("success-text");

  if (title) title.textContent = isOut ? "Гарсан цаг бүртгэгдлээ!" : "Амжилттай бүртгэгдлээ!";
  if (time) time.textContent = timeStr;
  if (text) text.textContent = isOut ? "Маргааш дахин уулзана 👋" : "Ажилд ирсэн цаг бүртгэгдлээ";

  showOverlay("success-overlay", true);
  setTimeout(() => showOverlay("success-overlay", false), 1800);
}

async function completeAction() {
  const now = new Date();
  const timeStr = `${String(now.getHours()).padStart(2, "0")}:${String(now.getMinutes()).padStart(2, "0")}`;

  if (!checkedIn) {
    await checkIn();
    checkedIn = true;
    checkInTime = now;
    document.getElementById("checkin-time").textContent = timeStr;
    document.getElementById("attendance-message").textContent = "Ирсэн бүртгэл хийгдлээ.";
    setButtonState("out");
    startWorkTimer();
    showSuccess(false, timeStr);
    return;
  }

  if (!checkedOut) {
    await checkOut();
    checkedOut = true;
    checkOutTime = now;
    document.getElementById("checkout-time").textContent = timeStr;
    document.getElementById("attendance-message").textContent = "Өнөөдрийн бүртгэл дууслаа.";
    setButtonState("done");
    stopWorkTimer();
    showSuccess(true, timeStr);
  }
}

function handleCheckin() {
  if (checkedOut) return;
  showOverlay("scan-overlay", true);

  const title = document.getElementById("scan-title");
  const text = document.getElementById("scan-text");
  if (title) title.textContent = "Баталгаажуулж байна...";
  if (text) text.textContent = "Хөдлөлгүй байна уу";

  setTimeout(async () => {
    showOverlay("scan-overlay", false);
    try {
      await completeAction();
    } catch (err) {
      alert(err?.message || "Үйлдэл амжилтгүй");
    }
  }, 1000);
}

function navTo(name) {
  document.querySelectorAll(".screen").forEach(s => s.classList.remove("active"));
  document.querySelectorAll(".nav-btn").forEach(b => b.classList.remove("active"));

  const screen = document.getElementById(`${name}-screen`);
  const nav = document.getElementById(`nav-${name}`);

  if (screen) screen.classList.add("active");
  if (nav) nav.classList.add("active");

  if (name === "stats") {
    loadStatistics();
  }
}

function openLeaveForm() {
  const el = document.getElementById('leave-overlay');
  if (el) el.classList.add('visible');
}

function closeLeaveForm() {
  const el = document.getElementById('leave-overlay');
  if (el) el.classList.remove('visible');
}

async function submitLeaveRequest(e) {
  e.preventDefault();
  const start = document.getElementById('leave-start').value;
  const end = document.getElementById('leave-end').value;
  const type = document.getElementById('leave-type').value;
  const reason = document.getElementById('leave-reason').value;

  if (!start || !end) {
    alert('Эхлэх болон дуусах огноог оруулна уу');
    return false;
  }

  // validate date order
  if (new Date(end) < new Date(start)) {
    alert('Дуусах огноо эхлэх огноогоос өмнө байх боломжгүй.');
    return false;
  }

  const btn = document.getElementById('leave-submit');
  if (btn) btn.disabled = true;

  try {
    if (!getAccessToken()) {
      alert('Та нэвтрээгүй байна. Нэвтрэх хуудсанд шилжих болно.');
      window.location.href = 'login.html';
      return false;
    }
    console.debug('Submitting leave request', { start, end, type, reason });
    const res = await apiFetch('/api/leave/requests', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ startDate: start, endDate: end, type, reason })
    });

    console.debug('Leave request response', res.status, res.statusText);

    if (!res.ok) {
      let txt = '';
      try { txt = await res.text(); } catch (_) { txt = res.statusText; }
      throw new Error(txt || `Чөлөө илгээхэд алдаа гарлаа (${res.status})`);
    }

    closeLeaveForm();
    const title = document.getElementById('success-title');
    const text = document.getElementById('success-text');
    const time = document.getElementById('success-time');
    if (title) title.textContent = 'Чөлөөний хүсэлт илгээгдлээ';
    if (text) text.textContent = 'Таны хүсэлтийг шалгана.';
    if (time) time.textContent = '';
    showOverlay('success-overlay', true);
    setTimeout(() => showOverlay('success-overlay', false), 1800);
  } catch (err) {
    alert(err?.message || 'Чөлөө илгээхэд алдаа');
  } finally {
    if (btn) btn.disabled = false;
  }

  return false;
}

async function initHome() {
  const name = getCurrentUserName();
  const departmentId = getCurrentDepartmentId();
  const empName = document.getElementById("emp-name");
  const departmentLabel = document.getElementById("department-id");
  if (empName) empName.textContent = name;
  if (departmentLabel) departmentLabel.textContent = departmentId;

  const now = new Date();
  const days = ["Ням", "Даваа", "Мягмар", "Лхагва", "Пүрэв", "Баасан", "Бямба"];
  const months = ["1-р сар", "2-р сар", "3-р сар", "4-р сар", "5-р сар", "6-р сар", "7-р сар", "8-р сар", "9-р сар", "10-р сар", "11-р сар", "12-р сар"];
  const date = document.getElementById("today-date");
  if (date) date.textContent = `${days[now.getDay()]}, ${months[now.getMonth()]} ${now.getDate()}`;

  buildCalendar();
  updateClock();
  setInterval(updateClock, 1000);

  if (!getAccessToken()) {
    window.location.href = "login.html";
    return;
  }

  const data = await loadTodayAttendance();
  checkedIn = !!data?.checkInTime;
  checkedOut = !!data?.checkOutTime;
  if (checkedIn && !checkedOut) {
    startWorkTimer();
  }
}

document.addEventListener("DOMContentLoaded", () => initHome().catch(err => {
  console.error(err);
  alert(err?.message || "Таны мэдээллийг ачааллахад алдаа гарлаа");
}));