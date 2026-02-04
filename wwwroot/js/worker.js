// ========== WORKER.JS - Complete Worker Dashboard ==========

const API_URL = "http://localhost:5286";
let currentUser = null;
let currentSession = null;
let timerInterval = null;
let pauseTimerInterval = null;
let pauseStartTime = null;

function checkAuth() {
    const userStr = localStorage.getItem("user");
    if (!userStr) {
        window.location.href = "/login.html";
        return null;
    }
    
    try {
        const user = JSON.parse(userStr);
        if (user.isAdmin === true) {
            window.location.href = "/admin.html";
            return null;
        }
        return user;
    } catch (err) {
        console.error("Error parsing user data:", err);
        localStorage.removeItem("user");
        window.location.href = "/login.html";
        return null;
    }
}

async function logout() {
    if (timerInterval) clearInterval(timerInterval);
    if (pauseTimerInterval) clearInterval(pauseTimerInterval);
    
    if (currentUser) {
        try {
            await fetch(`${API_URL}/api/worker/auto-end`, {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ userId: currentUser.id })
            });
        } catch (err) {
            console.error("Auto-end error:", err);
        }
    }
    
    localStorage.removeItem("user");
    window.location.href = "/login.html";
}

function formatMinutes(minutes) {
    if (!minutes || minutes === 0) return "0h 0m";
    const hours = Math.floor(minutes / 60);
    const mins = minutes % 60;
    return `${hours}h ${mins}m`;
}

function formatSeconds(totalSeconds) {
    const hours = Math.floor(totalSeconds / 3600);
    const minutes = Math.floor((totalSeconds % 3600) / 60);
    const seconds = totalSeconds % 60;
    return `${String(hours).padStart(2, '0')}:${String(minutes).padStart(2, '0')}:${String(seconds).padStart(2, '0')}`;
}

function formatDate(dateStr) {
    const date = new Date(dateStr);
    return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
}

function formatTime(dateStr) {
    if (!dateStr) return "-";
    const date = new Date(dateStr);
    return date.toLocaleTimeString('en-US', { hour: '2-digit', minute: '2-digit' });
}

function updateUI(session, needsApproval) {
    const btnStart = document.getElementById("btnStart");
    const btnPause = document.getElementById("btnPause");
    const btnResume = document.getElementById("btnResume");
    const btnEnd = document.getElementById("btnEnd");
    const statusIndicator = document.getElementById("statusIndicator");
    const timerLabel = document.getElementById("timerLabel");
    
    if (needsApproval) {
        btnStart.style.display = "none";
        btnPause.style.display = "none";
        btnResume.style.display = "none";
        btnEnd.style.display = "none";
        
        statusIndicator.className = "status-indicator status-idle";
        statusIndicator.querySelector(".status-text").innerText = "Approval Required";
        timerLabel.innerHTML = "You logged out earlier today.<br>Contact admin to approve re-login.";
        document.getElementById("liveTimer").innerText = "--:--:--";
        return;
    }
    
    if (!session) {
        btnStart.style.display = "inline-block";
        btnPause.style.display = "none";
        btnResume.style.display = "none";
        btnEnd.style.display = "none";
        
        statusIndicator.className = "status-indicator status-idle";
        statusIndicator.querySelector(".status-text").innerText = "Not Clocked In";
        timerLabel.innerText = "Click 'Start Work' to begin";
        
        if (timerInterval) {
            clearInterval(timerInterval);
            timerInterval = null;
        }
        if (pauseTimerInterval) {
            clearInterval(pauseTimerInterval);
            pauseTimerInterval = null;
        }
        document.getElementById("liveTimer").innerText = "00:00:00";
        
    } else if (session.isPaused) {
        btnStart.style.display = "none";
        btnPause.style.display = "none";
        btnResume.style.display = "inline-block";
        btnEnd.style.display = "inline-block";
        
        statusIndicator.className = "status-indicator status-paused";
        statusIndicator.querySelector(".status-text").innerText = "On Break";
        
        // Store pause start time
        pauseStartTime = new Date(session.currentPauseStartTime);
        
        // Stop work timer
        if (timerInterval) {
            clearInterval(timerInterval);
            timerInterval = null;
        }
        
        // Start pause timer
        startPauseTimer();
        
        timerLabel.innerText = `Paused since ${formatTime(session.currentPauseStartTime)}`;
        
    } else {
        btnStart.style.display = "none";
        btnPause.style.display = "inline-block";
        btnResume.style.display = "none";
        btnEnd.style.display = "inline-block";
        
        statusIndicator.className = "status-indicator status-working";
        statusIndicator.querySelector(".status-text").innerText = "Working";
        timerLabel.innerText = `Started at ${formatTime(session.startTime)}`;
        
        // Stop pause timer if running
        if (pauseTimerInterval) {
            clearInterval(pauseTimerInterval);
            pauseTimerInterval = null;
        }
        
        startLiveTimer(session);
    }
}

function startLiveTimer(session) {
    if (timerInterval) {
        clearInterval(timerInterval);
    }
    
    timerInterval = setInterval(() => {
        const now = new Date();
        const start = new Date(session.startTime);
        const totalSeconds = Math.floor((now - start) / 1000);
        const pausedSeconds = session.pausedMinutes * 60;
        const workedSeconds = totalSeconds - pausedSeconds;
        
        document.getElementById("liveTimer").innerText = formatSeconds(Math.max(0, workedSeconds));
    }, 1000);
}

function startPauseTimer() {
    if (pauseTimerInterval) {
        clearInterval(pauseTimerInterval);
    }
    
    pauseTimerInterval = setInterval(() => {
        if (!pauseStartTime) return;
        
        const now = new Date();
        const pausedSeconds = Math.floor((now - pauseStartTime) / 1000);
        
        // Show pause duration
        document.getElementById("liveTimer").innerText = "⏸️ " + formatSeconds(pausedSeconds);
    }, 1000);
}

async function loadCurrentSession() {
    if (!currentUser) return;
    
    try {
        const response = await fetch(`${API_URL}/api/worker/current-session?userId=${currentUser.id}`);
        const data = await response.json();
        
        if (data.hasSession) {
            currentSession = data.session;
        } else {
            currentSession = null;
        }
    } catch (err) {
        console.error("Error loading session:", err);
    }
}

async function startWork() {
    if (!currentUser) return;
    
    try {
        const response = await fetch(`${API_URL}/api/worker/start`, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ userId: currentUser.id })
        });
        
        if (!response.ok) {
            const error = await response.json();
            alert(error.error || "Failed to start work");
            return;
        }
        
        await loadCurrentSession();
        await loadWorkerData();
    } catch (err) {
        console.error("Start work error:", err);
        alert("Cannot connect to server");
    }
}

async function pauseWork() {
    if (!currentUser) return;
    
    try {
        const response = await fetch(`${API_URL}/api/worker/pause`, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ userId: currentUser.id })
        });
        
        if (!response.ok) {
            const error = await response.json();
            alert(error.error || "Failed to pause work");
            return;
        }
        
        await loadCurrentSession();
        await loadWorkerData();
    } catch (err) {
        console.error("Pause work error:", err);
        alert("Cannot connect to server");
    }
}

async function resumeWork() {
    if (!currentUser) return;
    
    try {
        const response = await fetch(`${API_URL}/api/worker/resume`, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ userId: currentUser.id })
        });
        
        if (!response.ok) {
            const error = await response.json();
            alert(error.error || "Failed to resume work");
            return;
        }
        
        await loadCurrentSession();
        await loadWorkerData();
    } catch (err) {
        console.error("Resume work error:", err);
        alert("Cannot connect to server");
    }
}

async function endWork() {
    if (!currentUser) return;
    
    const confirm = window.confirm("Are you sure you want to end your work day?");
    if (!confirm) return;
    
    try {
        const response = await fetch(`${API_URL}/api/worker/end`, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ userId: currentUser.id })
        });
        
        if (!response.ok) {
            const error = await response.json();
            alert(error.error || "Failed to end work");
            return;
        }
        
        const result = await response.json();
        alert(`Work day ended! Total: ${formatMinutes(result.totalWorkedMinutes)}`);
        
        await loadCurrentSession();
        await loadWorkerData();
    } catch (err) {
        console.error("End work error:", err);
        alert("Cannot connect to server");
    }
}

async function loadWorkerData() {
    if (!currentUser) return;
    
    try {
        const response = await fetch(`${API_URL}/api/worker/dashboard?userId=${currentUser.id}`);
        
        if (!response.ok) {
            throw new Error("Failed to fetch worker data");
        }
        
        const data = await response.json();
        
        // Update all stats
        document.getElementById("todayHours").innerText = formatMinutes(data.todayMinutes || 0);
        document.getElementById("weekHours").innerText = formatMinutes(data.weekMinutes || 0);
        document.getElementById("monthHours").innerText = formatMinutes(data.monthMinutes || 0);
        document.getElementById("pausedTime").innerText = formatMinutes(data.pausedMinutes || 0);
        document.getElementById("totalSessions").innerText = data.totalSessions || 0;
        
        // Week progress
        const weekMins = data.weekMinutes || 0;
        document.getElementById("weekProgress").innerText = `${formatMinutes(weekMins)} / 40h`;
        
        updateUI(currentSession, data.needsApproval || false);
        
    } catch (err) {
        console.error("Worker data error:", err);
    }
    
    await loadSessions();
}

async function loadSessions() {
    if (!currentUser) return;
    
    try {
        const response = await fetch(`${API_URL}/api/worker/sessions?userId=${currentUser.id}`);
        
        if (!response.ok) {
            throw new Error("Failed to fetch sessions");
        }
        
        const sessions = await response.json();
        const tbody = document.getElementById("sessionsTableBody");
        
        if (!sessions || sessions.length === 0) {
            tbody.innerHTML = '<tr><td colspan="5" class="loading">No work sessions yet</td></tr>';
            return;
        }
        
        tbody.innerHTML = sessions.map(session => {
            return `
                <tr>
                    <td>${formatDate(session.startTime)}</td>
                    <td>${formatTime(session.startTime)}</td>
                    <td>${formatTime(session.endTime)}</td>
                    <td><strong>${formatMinutes(session.workedMinutes || 0)}</strong></td>
                    <td>${formatMinutes(session.pausedMinutes || 0)}</td>
                </tr>
            `;
        }).join('');
        
    } catch (err) {
        console.error("Sessions error:", err);
        const tbody = document.getElementById("sessionsTableBody");
        tbody.innerHTML = '<tr><td colspan="5" class="loading">Error loading sessions</td></tr>';
    }
}

function refreshData() {
    loadCurrentSession();
    loadWorkerData();
}

document.addEventListener("DOMContentLoaded", () => {
    currentUser = checkAuth();
    if (!currentUser) return;
    
    document.getElementById("workerName").innerText = currentUser.username;
    
    loadCurrentSession().then(() => loadWorkerData());
    setInterval(() => {
        loadCurrentSession().then(() => loadWorkerData());
    }, 30000);
});