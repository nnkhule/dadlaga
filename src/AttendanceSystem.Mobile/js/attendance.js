const OFFICE_LOCATION = { latitude: 47.9120, longitude: 106.9308 };
const ATTENDANCE_RADIUS_METERS = 100;
const CAMERA_SCAN_DURATION = 2200;

function toRadians(degrees) {
  return degrees * (Math.PI / 180);
}

function getDistanceMeters(lat1, lon1, lat2, lon2) {
  const dLat = toRadians(lat2 - lat1);
  const dLon = toRadians(lon2 - lon1);
  const a =
    Math.sin(dLat / 2) * Math.sin(dLat / 2) +
    Math.cos(toRadians(lat1)) * Math.cos(toRadians(lat2)) *
    Math.sin(dLon / 2) * Math.sin(dLon / 2);
  const c = 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
  return 6371000 * c;
}

function wait(ms) {
  return new Promise(resolve => setTimeout(resolve, ms));
}

async function getLocation() {
  if (!navigator.geolocation) {
    throw new Error("Геолокаци дэмжигдэхгүй байна.");
  }

  return new Promise((resolve, reject) => {
    navigator.geolocation.getCurrentPosition(
      pos => resolve({
        latitude: pos.coords.latitude,
        longitude: pos.coords.longitude
      }),
      err => reject(new Error(err.message || "Байршил авахад алдаа гарлаа.")),
      {
        enableHighAccuracy: true,
        timeout: 12000,
        maximumAge: 0
      }
    );
  });
}

function formatTime(value) {
  if (!value) return "--:--";
  const date = new Date(value);
  return date.toLocaleTimeString("en-GB", { hour: "2-digit", minute: "2-digit" });
}

function setAttendanceStatus(status) {
  const statusEl = document.getElementById("attendance-status");
  if (!statusEl) return;

  const statusMap = {
    Present: "Ирсэн",
    CheckedIn: "Ирсэн",
    CheckedOut: "Гарсан",
    Late: "Хоцорсон",
    Absent: "Гүйцэтгээгүй",
    Pending: "Хүлээгдэж буй"
  };

  statusEl.textContent = statusMap[status] || status || "Бүртгэл хийгдээгүй";
}

async function loadTodayAttendance() {
  const res = await apiFetch("/api/attendance/today");

  if (!res.ok) {
    document.getElementById("checkin-time").textContent = "--:--";
    document.getElementById("checkout-time").textContent = "--:--";
    document.getElementById("attendance-message").textContent = "Өнөөдрийн бүртгэл олдсонгүй.";
    setButtonState("in");
    setAttendanceStatus("Pending");
    return null;
  }

  const data = await res.json();
  document.getElementById("checkin-time").textContent = formatTime(data?.checkInTime);
  document.getElementById("checkout-time").textContent = formatTime(data?.checkOutTime);

  if (data?.checkInTime && data?.checkOutTime) {
    checkedIn = true;
    checkedOut = true;
    checkInTime = data.checkInTime ? new Date(data.checkInTime) : null;
    checkOutTime = data.checkOutTime ? new Date(data.checkOutTime) : null;
    setButtonState("done");
    document.getElementById("attendance-message").textContent = "Өнөөдрийн бүртгэл дууссан.";
    setAttendanceStatus(data.status || "CheckedOut");
  } else if (data?.checkInTime) {
    checkedIn = true;
    checkedOut = false;
    checkInTime = data.checkInTime ? new Date(data.checkInTime) : null;
    checkOutTime = null;
    setButtonState("out");
    document.getElementById("attendance-message").textContent = "Ирсэн бүртгэл хийгдсэн байна.";
    setAttendanceStatus(data.status || "CheckedIn");
  } else {
    checkedIn = false;
    checkedOut = false;
    checkInTime = null;
    checkOutTime = null;
    setButtonState("in");
    document.getElementById("attendance-message").textContent = "Дарж ажилд ирсэн бүртгэлээ хийнэ үү";
    setAttendanceStatus("Pending");
  }

  return data;
}

async function openCameraScanner(action) {
  const overlay = document.getElementById("scan-overlay");
  const video = document.getElementById("camera-video");
  const status = document.getElementById("scan-status");
  const title = document.getElementById("scan-title");
  const text = document.getElementById("scan-text");

  if (!overlay || !video || !status || !title || !text) {
    throw new Error("Камерын интерфейс алдаатай байна.");
  }

  title.textContent = action === "checkin" ? "Нүүр таних" : "Гарах нүүр таних";
  text.textContent = "Камер нээгдэж байна. Урд тал руу харна уу.";
  status.textContent = "Скан хийж байна...";

  try {
    const stream = await navigator.mediaDevices.getUserMedia({
      video: { facingMode: "user" },
      audio: false
    });

    video.srcObject = stream;
    await video.play();
    overlay.classList.add("visible");

    await wait(CAMERA_SCAN_DURATION);
    status.textContent = "Танилцаах явцдаа...";
    await wait(700);

    const canvas = document.createElement("canvas");
    canvas.width = video.videoWidth || 480;
    canvas.height = video.videoHeight || 640;
    const ctx = canvas.getContext("2d");
    if (ctx) {
      ctx.drawImage(video, 0, 0, canvas.width, canvas.height);
    }

    stream.getTracks().forEach(track => track.stop());
    overlay.classList.remove("visible");

    return canvas.toDataURL("image/jpeg", 0.75);
  } catch (err) {
    overlay.classList.remove("visible");
    throw new Error(err?.message || "Камер нээх үед алдаа гарлаа.");
  }
}

async function showGpsVerification(distance) {
  const overlay = document.getElementById("gps-overlay");
  const distanceEl = document.getElementById("gps-distance");
  const badge = document.getElementById("gps-target");

  if (!overlay || !distanceEl || !badge) return;

  distanceEl.textContent = `${Math.round(distance)} м`;
  badge.textContent = "Оффис: 100 м хүртэл";
  overlay.classList.add("visible");

  await wait(1400);
  overlay.classList.remove("visible");
}

function showDistanceBlocked(distance) {
  const overlay = document.getElementById("distance-overlay");
  const msg = document.getElementById("distance-message");
  if (!overlay || !msg) return;

  msg.textContent = `Оффисоос ${Math.round(distance)} метрийн зайтай байна. 100 м-ээс дотогш байх шаардлагатай.`;
  overlay.classList.add("visible");
}

function closeDistanceOverlay() {
  const overlay = document.getElementById("distance-overlay");
  if (overlay) overlay.classList.remove("visible");
}

async function runAttendanceFlow(action) {
  const photoBase64 = await openCameraScanner(action);
  const location = await getLocation();
  const distance = getDistanceMeters(
    location.latitude,
    location.longitude,
    OFFICE_LOCATION.latitude,
    OFFICE_LOCATION.longitude
  );

  if (distance > 100) {
    showDistanceBlocked(distance);
    throw new Error(`Таны зай ${Math.round(distance)} м байна. Оффисоос 100 м-ээс дотогш байх шаардлагатай.`);
  }

  await showGpsVerification(distance);

  const endpoint = action === "checkin" ? "/api/attendance/checkin" : "/api/attendance/checkout";
  const body = {
    latitude: location.latitude,
    longitude: location.longitude,
    verificationMethod: "Gps"
  };

  if (action === "checkin") {
    body.photoBase64 = photoBase64;
    body.locationPermissionGranted = true;
  }


    console.debug("Attendance payload", { action, location, distance, body });

  const res = await apiFetch(endpoint, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body)
  });

  if (!res.ok) {
    const errorData = await parseResponseText(res);
    const message = errorData?.message || errorData || `${action === "checkin" ? "Ирэх" : "Гарах"} бүртгэл амжилтгүй.`;
    throw new Error(message);
  }

  return true;
}

function handleAttendanceAction() {
  if (checkedOut) return;

  const action = checkedIn ? "checkout" : "checkin";
  runAttendanceFlow(action)
    .then(() => {
      if (!checkedIn) {
        checkedIn = true;
        checkInTime = new Date();
        document.getElementById("checkin-time").textContent = formatTime(checkInTime);
        document.getElementById("attendance-message").textContent = "Ирсэн бүртгэл хийгдлээ.";
        setButtonState("out");
        startWorkTimer();
        showSuccess(false, formatTime(checkInTime));
      } else {
        checkedOut = true;
        checkOutTime = new Date();
        document.getElementById("checkout-time").textContent = formatTime(checkOutTime);
        document.getElementById("attendance-message").textContent = "Өнөөдрийн бүртгэл дууслаа.";
        setButtonState("done");
        stopWorkTimer();
        showSuccess(true, formatTime(checkOutTime));
      }
    })
    .catch(err => {
      alert(err?.message || "Үйлдэл амжилтгүй боллоо.");
    });
}

async function loadStatistics() {
  const res = await apiFetch("/api/attendance/statistics");
  if (!res.ok) return;

  const data = await res.json();
  const absent = data.workingDays - data.presentDays;

  document.getElementById("presentDays").textContent = `${data.presentDays}/${data.workingDays}`;
  document.getElementById("lateCount").textContent = data.lateCount;
  document.getElementById("absentDays").textContent = absent < 0 ? "0" : absent;
  document.getElementById("overtimeHours").textContent = `${data.overtimeHours}ц`;

  const monthEl = document.getElementById("stats-month");
  if (monthEl && data.monthLabel) monthEl.textContent = `(${data.monthLabel})`;

  const list = document.getElementById("history-list");
  if (list) {
    list.innerHTML = "";
    (data.recentRecords || []).forEach(item => {
      const date = item.date ? new Date(item.date) : null;
      const dayLabel = date
        ? date.toLocaleDateString(undefined, { weekday: 'short', month: 'numeric', day: 'numeric' })
        : '';

      const checkIn = item.checkInTime ? formatTime(item.checkInTime) : '--:--';
      const checkOut = item.checkOutTime ? formatTime(item.checkOutTime) : '--:--';

      const el = document.createElement('div');
      el.className = 'history-item';
      el.innerHTML = `<span>${dayLabel}</span><b>${checkIn} → ${checkOut}</b>`;
      list.appendChild(el);
    });
  }
}
