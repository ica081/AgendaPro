const API_URL = "https://agendaapi-4772.onrender.com";

// ====== VARIÁVEL GLOBAL PARA A SEMANA ATUAL ======
let currentWeekStart = null;

function getToken() {
    return localStorage.getItem("token");
}

function getCompanyId() {
    if (window.selectedCompanyId) {
        return window.selectedCompanyId;
    }
    return localStorage.getItem("companyId");
}

// ====== FUNÇÕES DE NAVEGAÇÃO SEMANAL ======
function getWeekStart(date) {
    const d = new Date(date);
    d.setHours(0, 0, 0, 0);
    const day = d.getDay(); // 0 = domingo
    d.setDate(d.getDate() - day);
    return d;
}

function setWeekStart(date) {
    currentWeekStart = getWeekStart(date);
    const input = document.getElementById('weekStartDate');
    if (input) {
        input.value = currentWeekStart.toISOString().split('T')[0];
    }
    loadWeek();
}

function loadCurrentWeek() {
    setWeekStart(new Date());
}

function previousWeek() {
    if (!currentWeekStart) {
        currentWeekStart = getWeekStart(new Date());
    }
    const newDate = new Date(currentWeekStart);
    newDate.setDate(newDate.getDate() - 7);
    setWeekStart(newDate);
}

function nextWeek() {
    if (!currentWeekStart) {
        currentWeekStart = getWeekStart(new Date());
    }
    const newDate = new Date(currentWeekStart);
    newDate.setDate(newDate.getDate() + 7);
    setWeekStart(newDate);
}

function loadWeekFromDate() {
    const dateInput = document.getElementById('weekStartDate');
    if (dateInput && dateInput.value) {
        const date = new Date(dateInput.value + 'T00:00:00');
        setWeekStart(date);
    }
}

// ====== FUNÇÃO PRINCIPAL ======
async function loadWeek() {
    // Se não houver currentWeekStart, define como hoje
    if (!currentWeekStart) {
        currentWeekStart = getWeekStart(new Date());
        const input = document.getElementById('weekStartDate');
        if (input) {
            input.value = currentWeekStart.toISOString().split('T')[0];
        }
    }

    const companyId = getCompanyId();
    const token = getToken();

    if (!companyId) {
        alert("Empresa não selecionada.");
        return;
    }

    if (!token) {
        alert("Token não encontrado. Faça login.");
        window.location.href = "/shared/login.html";
        return;
    }

    let company = null;

    try {
        const companyRes = await fetch(`${API_URL}/companies`, {
            headers: { "Authorization": "Bearer " + token }
        });
        if (companyRes.ok) {
            const companies = await companyRes.json();
            company = companies.find(c => c.id === companyId);
        }
    } catch (e) {
        console.warn("Erro ao buscar empresas via /companies:", e);
    }

    if (!company) {
        try {
            const resp = await fetch(`${API_URL}/public/company/${companyId}`);
            if (!resp.ok) {
                alert("Empresa não encontrada.");
                return;
            }
            company = await resp.json();
        } catch (e) {
            alert("Erro ao buscar empresa.");
            return;
        }
    }

    if (!company || !company.workSchedule) {
        alert("Configuração de horários não encontrada.");
        return;
    }

    const schedule = company.workSchedule;
    const step = schedule.stepMinutes || 30;

    // ====== USAR currentWeekStart ======
    const startOfWeek = new Date(currentWeekStart);
    const endOfWeek = new Date(startOfWeek);
    endOfWeek.setDate(startOfWeek.getDate() + 6);

    // Buscar agendamentos da semana
    const params = new URLSearchParams({
        start: startOfWeek.toISOString().split('T')[0],
        end: endOfWeek.toISOString().split('T')[0]
    });

    const appRes = await fetch(`${API_URL}/companies/${companyId}/schedules/week?${params}`, {
        headers: { "Authorization": "Bearer " + token }
    });
    if (!appRes.ok) {
        alert("Erro ao carregar agendamentos.");
        return;
    }
    const appointments = await appRes.json();

    // Mapa de agendamentos com chave local
    const appointmentMap = {};
    appointments.forEach(app => {
        const localDate = new Date(app.startTime);
        const localDateStr = localDate.getFullYear() + '-' +
                             String(localDate.getMonth() + 1).padStart(2, '0') + '-' +
                             String(localDate.getDate()).padStart(2, '0');
        const localTimeStr = String(localDate.getHours()).padStart(2, '0') + ':' +
                             String(localDate.getMinutes()).padStart(2, '0');
        const key = localDateStr + 'T' + localTimeStr;
        appointmentMap[key] = app;
    });

    const container = document.getElementById('week-container');
    if (!container) {
        console.error("Elemento #week-container não encontrado.");
        return;
    }

    let html = '<table><thead><tr><th>Horário</th>';
    const days = ['Dom', 'Seg', 'Ter', 'Qua', 'Qui', 'Sex', 'Sáb'];
    for (let i = 0; i < 7; i++) {
        const d = new Date(startOfWeek);
        d.setDate(startOfWeek.getDate() + i);
        html += `<th>${days[i]} ${d.getDate()}/${d.getMonth() + 1}</th>`;
    }
    html += '</tr></thead><tbody>';

    // Gerar slots para cada dia
    for (let i = 0; i < 7; i++) {
        const date = new Date(startOfWeek);
        date.setDate(startOfWeek.getDate() + i);
        const dateStr = date.getFullYear() + '-' +
                        String(date.getMonth() + 1).padStart(2, '0') + '-' +
                        String(date.getDate()).padStart(2, '0');
        const dayOfWeek = date.getDay();

        let periods = [];
        let isClosed = false;
        if (schedule.exceptions && schedule.exceptions[dateStr]) {
            const ex = schedule.exceptions[dateStr];
            if (ex.isClosed) isClosed = true;
            else periods = ex.periods;
        } else {
            const dayConfig = schedule.days[dayOfWeek];
            if (dayConfig) {
                if (dayConfig.isClosed) isClosed = true;
                else periods = dayConfig.periods;
            }
        }

        const daySlots = [];
        if (!isClosed && periods.length > 0) {
            periods.forEach(p => {
                let current = new Date(date);
                const [sh, sm] = p.start.split(':').map(Number);
                const [eh, em] = p.end.split(':').map(Number);
                current.setHours(sh, sm, 0, 0);
                const end = new Date(date);
                end.setHours(eh, em, 0, 0);
                while (current < end) {
                    daySlots.push(current.toTimeString().slice(0, 5));
                    current.setMinutes(current.getMinutes() + step);
                }
            });
        }
        window._daySlots = window._daySlots || {};
        window._daySlots[dateStr] = daySlots;
    }

    const allSlots = new Set();
    for (let i = 0; i < 7; i++) {
        const date = new Date(startOfWeek);
        date.setDate(startOfWeek.getDate() + i);
        const dateStr = date.getFullYear() + '-' +
                        String(date.getMonth() + 1).padStart(2, '0') + '-' +
                        String(date.getDate()).padStart(2, '0');
        const slots = window._daySlots[dateStr] || [];
        slots.forEach(s => allSlots.add(s));
    }
    const sortedSlots = Array.from(allSlots).sort();

    sortedSlots.forEach(timeStr => {
        html += `<tr><td class="hour-cell">${timeStr}</td>`;
        for (let i = 0; i < 7; i++) {
            const date = new Date(startOfWeek);
            date.setDate(startOfWeek.getDate() + i);
            const dateStr = date.getFullYear() + '-' +
                            String(date.getMonth() + 1).padStart(2, '0') + '-' +
                            String(date.getDate()).padStart(2, '0');
            const daySlots = window._daySlots[dateStr] || [];
            const isSlot = daySlots.includes(timeStr);
            const key = dateStr + 'T' + timeStr;
            const appointment = appointmentMap[key];
            let cell = '';
            if (appointment) {
                const employeeName = appointment.EmployeeName || appointment.employeeName || '';
                cell = `<span class="appointment">${appointment.clientName}${employeeName ? ` (${employeeName})` : ''}</span>`;
            } else if (isSlot) {
                cell = '✓';
            } else {
                cell = '';
            }
            html += `<td>${cell}</td>`;
        }
        html += '</tr>';
    });

    html += '</tbody></table>';
    container.innerHTML = html;
}