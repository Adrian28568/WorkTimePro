// ========== ADMIN.JS - Complete HR Dashboard ==========

const API_URL = "http://localhost:5286";

function checkAuth() {
    const userStr = localStorage.getItem("user");
    if (!userStr) {
        window.location.href = "/login.html";
        return null;
    }
    
    const user = JSON.parse(userStr);
    if (!user.isAdmin) {
        alert("Access denied. Admin only.");
        window.location.href = "/login.html";
        return null;
    }
    
    return user;
}

function logout() {
    localStorage.removeItem("user");
    window.location.href = "/login.html";
}

function formatMinutes(minutes) {
    if (!minutes || minutes === 0) return "0m";
    const hours = Math.floor(minutes / 60);
    const mins = minutes % 60;
    if (hours === 0) return `${mins}m`;
    if (mins === 0) return `${hours}h`;
    return `${hours}h ${mins}m`;
}

function formatEuro(amount) {
    return `€${amount.toFixed(2)}`;
}

async function loadDashboard() {
    const user = checkAuth();
    if (!user) return;
    
    document.getElementById("adminName").innerText = `Admin: ${user.username}`;
    
    try {
        const response = await fetch(`${API_URL}/api/admin/dashboard`);
        
        if (!response.ok) {
            throw new Error("Failed to fetch dashboard");
        }
        
        const data = await response.json();
        
        // Update cards in new order
        document.getElementById("totalWorkers").innerText = data.totalWorkers || 0;
        document.getElementById("totalMinutesToday").innerText = formatMinutes(data.totalMinutesToday || 0);
        document.getElementById("monthlyPayroll").innerText = formatEuro(data.monthlyPayroll || 0);
        
        // Update status row
        document.getElementById("workingNow").innerText = data.currentlyWorking || 0;
        document.getElementById("pausedNow").innerText = data.currentlyPaused || 0;
        document.getElementById("notClockedIn").innerText = data.notClockedIn || 0;
        
    } catch (err) {
        console.error("Dashboard error:", err);
    }
    
    await loadWorkers();
}

async function loadWorkers() {
    try {
        const response = await fetch(`${API_URL}/api/admin/workers`);
        
        if (!response.ok) {
            throw new Error("Failed to fetch workers");
        }
        
        const workers = await response.json();
        const tbody = document.getElementById("workersTableBody");
        
        if (!workers || workers.length === 0) {
            tbody.innerHTML = '<tr><td colspan="5" class="loading">No workers found</td></tr>';
            return;
        }
        
        tbody.innerHTML = workers.map(worker => {
            const status = worker.isWorking 
                ? '<span class="status-badge status-working">Working</span>'
                : worker.isPaused 
                    ? '<span class="status-badge status-paused">Paused</span>'
                    : '<span class="status-badge status-idle">Idle</span>';
            
            return `
                <tr onclick="openWorkerDetails(${worker.id}, '${worker.username}')" class="worker-row">
                    <td><strong>${worker.username}</strong></td>
                    <td>${status}</td>
                    <td>${formatMinutes(worker.todayMinutes || 0)}</td>
                    <td>${formatMinutes(worker.weekMinutes || 0)}</td>
                    <td><strong>${formatEuro(worker.monthEarnings || 0)}</strong></td>
                </tr>
            `;
        }).join('');
        
    } catch (err) {
        console.error("Workers error:", err);
        const tbody = document.getElementById("workersTableBody");
        tbody.innerHTML = '<tr><td colspan="5" class="loading">Error loading workers</td></tr>';
    }
}

async function openWorkerDetails(workerId, workerName) {
    const modal = document.getElementById("workerModal");
    const modalBody = document.getElementById("modalBody");
    
    document.getElementById("modalWorkerName").innerText = workerName;
    modal.style.display = "block";
    modalBody.innerHTML = '<p class="loading">Loading worker details...</p>';
    
    try {
        const now = new Date();
        const response = await fetch(`${API_URL}/api/admin/worker/${workerId}/details?month=${now.getMonth() + 1}&year=${now.getFullYear()}`);
        
        if (!response.ok) {
            const errorText = await response.text();
            console.error("Worker details error:", errorText);
            throw new Error("Failed to fetch worker details");
        }
        
        const data = await response.json();
        
        console.log("Worker details data:", data); // Debug
        
        let html = `
            <div class="worker-details">
                <div class="detail-summary">
                    <h3>This Month Summary</h3>
                    <div class="summary-grid">
                        <div class="summary-item">
                            <span class="summary-label">Total Time:</span>
                            <span class="summary-value">${data.monthSummary.totalHours || '0h 0m'}</span>
                        </div>
                        <div class="summary-item">
                            <span class="summary-label">Total Earned:</span>
                            <span class="summary-value">${formatEuro(data.monthSummary.totalEarned || 0)}</span>
                        </div>
                        <div class="summary-item">
                            <span class="summary-label">Days Worked:</span>
                            <span class="summary-value">${data.monthSummary.daysWorked || 0}</span>
                        </div>
                        <div class="summary-item">
                            <span class="summary-label">Days Missed:</span>
                            <span class="summary-value">${data.monthSummary.daysMissed || 0}</span>
                        </div>
                    </div>
                </div>
                
                <h3>Daily Breakdown</h3>
                <div class="calendar-table">
                    <table class="workers-table">
                        <thead>
                            <tr>
                                <th>Date</th>
                                <th>Day</th>
                                <th>Start</th>
                                <th>End</th>
                                <th>Worked</th>
                                <th>Status</th>
                            </tr>
                        </thead>
                        <tbody>
        `;
        
        if (data.dailyBreakdown && data.dailyBreakdown.length > 0) {
            data.dailyBreakdown.forEach(day => {
                const rowClass = day.status === 'No Clock-In' ? 'missed-day' : day.isLongDay ? 'long-day' : '';
                html += `
                    <tr class="${rowClass}">
                        <td>${day.date}</td>
                        <td>${day.dayOfWeek}</td>
                        <td>${day.startTime || '-'}</td>
                        <td>${day.endTime || '-'}</td>
                        <td><strong>${day.workedDisplay || '0h 0m'}</strong></td>
                        <td>${day.status}</td>
                    </tr>
                `;
            });
        } else {
            html += `
                <tr>
                    <td colspan="6" class="loading">No data for this month yet</td>
                </tr>
            `;
        }
        
        html += `
                        </tbody>
                    </table>
                </div>
            </div>
        `;
        
        modalBody.innerHTML = html;
        
    } catch (err) {
        console.error("Worker details error:", err);
        modalBody.innerHTML = `<p class="error-message">Error loading worker details: ${err.message}</p>`;
    }
}

function closeWorkerModal() {
    document.getElementById("workerModal").style.display = "none";
}

async function openPayrollOverview() {
    const modal = document.getElementById("payrollModal");
    const modalBody = document.getElementById("payrollModalBody");
    
    modal.style.display = "block";
    modalBody.innerHTML = '<p class="loading">Loading payroll overview...</p>';
    
    try {
        const now = new Date();
        const response = await fetch(`${API_URL}/api/admin/payroll-overview?month=${now.getMonth() + 1}&year=${now.getFullYear()}`);
        
        if (!response.ok) {
            const errorText = await response.text();
            console.error("Payroll error response:", errorText);
            throw new Error("Failed to fetch payroll");
        }
        
        const data = await response.json();
        
        console.log("Payroll data:", data); // Debug
        
        let html = `
            <div class="payroll-overview">
                <div class="payroll-header">
                    <h3>Payroll for ${data.monthName} ${data.year}</h3>
                    <button onclick="exportPayrollCSV()" class="btn-export">📥 Export to CSV</button>
                </div>
                
                <table class="workers-table">
                    <thead>
                        <tr>
                            <th>Worker</th>
                            <th>Days Worked</th>
                            <th>Total Time</th>
                            <th>Hourly Rate</th>
                            <th>Total Earned</th>
                        </tr>
                    </thead>
                    <tbody>
        `;
        
        if (data.workers && data.workers.length > 0) {
            data.workers.forEach(w => {
                html += `
                    <tr>
                        <td><strong>${w.username}</strong></td>
                        <td>${w.daysWorked}</td>
                        <td>${w.totalHours}</td>
                        <td>${formatEuro(w.hourlyRate)}/h</td>
                        <td><strong>${formatEuro(w.totalEarned)}</strong></td>
                    </tr>
                `;
            });
        } else {
            html += `
                <tr>
                    <td colspan="5" class="loading">No payroll data for this month</td>
                </tr>
            `;
        }
        
        html += `
                    </tbody>
                    <tfoot>
                        <tr class="total-row">
                            <td colspan="4"><strong>TOTAL MONTHLY PAYROLL</strong></td>
                            <td><strong>${formatEuro(data.grandTotal || 0)}</strong></td>
                        </tr>
                    </tfoot>
                </table>
                
                <p class="payroll-note">* Calculated at €0.216 per minute (€13/hour ÷ 60 minutes)</p>
            </div>
        `;
        
        modalBody.innerHTML = html;
        window.currentPayrollData = data;
        
    } catch (err) {
        console.error("Payroll error:", err);
        modalBody.innerHTML = `<p class="error-message">Error loading payroll overview: ${err.message}<br><br>Make sure workers have HourlyRate set in database.</p>`;
    }
}

function closePayrollModal() {
    document.getElementById("payrollModal").style.display = "none";
}

function exportPayrollCSV() {
    if (!window.currentPayrollData) return;
    
    const data = window.currentPayrollData;
    let csv = "Worker,Days Worked,Total Time,Hourly Rate,Total Earned\n";
    
    data.workers.forEach(w => {
        csv += `${w.username},${w.daysWorked},${w.totalHours},€${w.hourlyRate},€${w.totalEarned}\n`;
    });
    
    csv += `\nTOTAL,,,,€${data.grandTotal}`;
    
    const blob = new Blob([csv], { type: 'text/csv' });
    const url = window.URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `payroll_${data.monthName}_${data.year}.csv`;
    a.click();
}

window.onclick = function(event) {
    const workerModal = document.getElementById("workerModal");
    const payrollModal = document.getElementById("payrollModal");
    
    if (event.target == workerModal) {
        closeWorkerModal();
    }
    if (event.target == payrollModal) {
        closePayrollModal();
    }
}

function refreshData() {
    loadDashboard();
}

document.addEventListener("DOMContentLoaded", () => {
    checkAuth();
    loadDashboard();
    setInterval(loadDashboard, 30000);
});