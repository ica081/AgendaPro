const API_URL = "https://agendaapi-4772.onrender.com";

function getToken() {
    return localStorage.getItem("token");
}

function getCompanyId() {
    return localStorage.getItem("companyId");
}

let selectedTimeSlot = null;
let currentDate = null;

window.onload = function() {
    console.log("Schedule page loaded");
    const dateInput = document.getElementById("selectedDate");
    if (dateInput) {
        const today = new Date().toISOString().split("T")[0];
        dateInput.value = today;
        currentDate = today;
    }
    loadServices();
    loadEmployees();
    loadDay();
    loadEditServices();
    loadEditEmployees();
};

// =======================
// LOAD SERVICES & EMPLOYEES
// =======================
async function loadServices() {
    const companyId = getCompanyId();
    const token = getToken();
    if (!companyId || !token) return;

    try {
        const response = await fetch(`${API_URL}/companies/${companyId}/services`, {
            headers: { "Authorization": "Bearer " + token }
        });
        if (!response.ok) throw new Error(`Erro ${response.status}`);
        const data = await response.json();
        const select = document.getElementById("services");
        if (!select) return;
        select.innerHTML = "";
        if (!Array.isArray(data) || data.length === 0) {
            select.innerHTML = '<option disabled>Nenhum serviço</option>';
            return;
        }
        data.forEach(s => {
            const opt = document.createElement("option");
            opt.value = s.id;
            opt.textContent = `${s.name} (${s.durationMinutes} min)`;
            select.appendChild(opt);
        });
    } catch (error) {
        console.error("Erro ao carregar serviços:", error);
    }
}

async function loadEmployees() {
    const companyId = getCompanyId();
    const token = getToken();
    if (!companyId || !token) return;

    try {
        const response = await fetch(`${API_URL}/employees`, {
            headers: { "Authorization": "Bearer " + token }
        });
        if (!response.ok) throw new Error(`Erro ${response.status}`);
        const data = await response.json();
        const select = document.getElementById("employees");
        if (!select) return;
        select.innerHTML = "";
        if (!Array.isArray(data) || data.length === 0) {
            select.innerHTML = '<option disabled>Nenhum funcionário</option>';
            return;
        }
        data.forEach(e => {
            const opt = document.createElement("option");
            opt.value = e.id;
            opt.textContent = e.name + (e.specialty ? ` (${e.specialty})` : "");
            select.appendChild(opt);
        });
    } catch (error) {
        console.error("Erro ao carregar funcionários:", error);
    }
}

// =======================
// LOAD DAY
// =======================
async function loadDay() {
    const companyId = getCompanyId();
    const token = getToken();
    if (!companyId || !token) {
        alert("Você precisa estar logado.");
        return;
    }

    const dateInput = document.getElementById("selectedDate");
    if (!dateInput) {
        alert("Campo de data não encontrado.");
        return;
    }
    const date = dateInput.value;
    if (!date) {
        alert("Selecione uma data.");
        return;
    }
    currentDate = date;

    try {
        const slotsResp = await fetch(`${API_URL}/companies/${companyId}/slots?date=${date}`, {
            headers: { "Authorization": "Bearer " + token }
        });
        if (!slotsResp.ok) {
            const errorText = await slotsResp.text();
            throw new Error(`Erro ao buscar slots: ${slotsResp.status} - ${errorText}`);
        }
        const slots = await slotsResp.json();

        const appRes = await fetch(`${API_URL}/companies/${companyId}/schedules?_t=${Date.now()}`, {
            headers: { "Authorization": "Bearer " + token }
        });
        if (!appRes.ok) {
            const errorText = await appRes.text();
            throw new Error(`Erro ao buscar agendamentos: ${appRes.status} - ${errorText}`);
        }
        const allAppointments = await appRes.json();
        const dayAppointments = allAppointments.filter(a => a.startTime && a.startTime.startsWith(date));

        const grid = document.getElementById("time-grid");
        if (!grid) return;
        grid.innerHTML = "";

        if (slots.length === 0) {
            grid.innerHTML = "<p style='grid-column:1/-1; text-align:center;'>Empresa fechada neste dia.</p>";
        } else {
            slots.forEach(slotTime => {
                const isOccupied = dayAppointments.some(a => {
                    const start = new Date(a.startTime);
                    return start.toTimeString().slice(0, 5) === slotTime;
                });

                const div = document.createElement("div");
                div.className = `time-slot ${isOccupied ? 'occupied' : ''}`;
                div.textContent = slotTime;
                div.dataset.time = slotTime;
                if (!isOccupied) {
                    div.onclick = function() {
                        selectTimeSlot(this, slotTime);
                    };
                }
                grid.appendChild(div);
            });
        }

        const calendar = document.getElementById("calendar");
        if (!calendar) return;
        calendar.innerHTML = "";
        if (dayAppointments.length === 0) {
            calendar.innerHTML = "<p>Sem agendamentos para esta data.</p>";
        } else {
            dayAppointments.sort((a, b) => new Date(a.startTime) - new Date(b.startTime));
            dayAppointments.forEach(s => {
                const div = document.createElement("div");
                div.className = "event";
                div.innerHTML = `
                    <strong>${new Date(s.startTime).toLocaleTimeString([], {hour: '2-digit', minute:'2-digit'})}</strong>
                    - ${s.clientName}
                    ${s.employeeName ? `(Funcionário: ${s.employeeName})` : ''}
                    <button class="btn-edit" onclick="openEditModal('${s.id}')">Editar</button>
                    <button class="btn-cancel" onclick="cancelSchedule('${s.id}')">Cancelar</button>
                `;
                calendar.appendChild(div);
            });
        }

        selectedTimeSlot = null;
        const form = document.getElementById("form-container");
        if (form) form.style.display = "none";
        const timeHidden = document.getElementById("selectedTime");
        if (timeHidden) timeHidden.value = "";

    } catch (error) {
        console.error("Erro ao carregar dia:", error);
        alert("Erro ao carregar horários: " + error.message);
    }
}

function selectTimeSlot(element, time) {
    document.querySelectorAll('.time-slot.selected').forEach(el => el.classList.remove('selected'));
    element.classList.add('selected');
    selectedTimeSlot = time;
    const timeHidden = document.getElementById("selectedTime");
    if (timeHidden) timeHidden.value = time;
    const form = document.getElementById("form-container");
    if (form) {
        form.style.display = "block";
        form.scrollIntoView({ behavior: 'smooth' });
    }
}

function cancelForm() {
    const form = document.getElementById("form-container");
    if (form) form.style.display = "none";
    document.querySelectorAll('.time-slot.selected').forEach(el => el.classList.remove('selected'));
    selectedTimeSlot = null;
    const timeHidden = document.getElementById("selectedTime");
    if (timeHidden) timeHidden.value = "";
}

// =======================
// CREATE SCHEDULE (com validações)
// =======================
async function createSchedule() {
    console.log("createSchedule iniciada");

    const companyId = getCompanyId();
    const token = getToken();
    if (!companyId || !token) {
        alert("Você precisa estar logado e ter uma empresa.");
        return;
    }

    const serviceSelect = document.getElementById("services");
    const employeeSelect = document.getElementById("employees");
    const nameInput = document.getElementById("clientName");
    const emailInput = document.getElementById("clientEmail");
    const timeHidden = document.getElementById("selectedTime");

    if (!serviceSelect || !employeeSelect || !nameInput || !emailInput || !timeHidden) {
        alert("Erro: alguns campos não foram encontrados. Recarregue a página.");
        return;
    }

    const serviceId = serviceSelect.value;
    const employeeId = employeeSelect.value;
    const clientName = nameInput.value.trim();
    const clientEmail = emailInput.value.trim();
    const startTime = timeHidden.value;

    if (!serviceId || !employeeId || !clientName || !clientEmail || !startTime) {
        alert("Preencha todos os campos e selecione um horário.");
        return;
    }

    const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    if (!emailRegex.test(clientEmail)) {
        alert("Por favor, insira um e-mail válido.");
        return;
    }

    const date = document.getElementById("selectedDate").value;
    if (!date) {
        alert("Data inválida.");
        return;
    }

    const startDateTime = new Date(date + 'T' + startTime);
    if (isNaN(startDateTime.getTime())) {
        alert("Data/hora inválida.");
        return;
    }
    const isoStart = startDateTime.toISOString();

    // AGORA ENVIA clientEmail (não clientPhone)
    const body = {
        serviceId: serviceId,
        employeeId: employeeId,
        clientName: clientName,
        clientEmail: clientEmail,   // <-- mudança aqui
        startTime: isoStart
    };

    console.log("Enviando body:", body);

    try {
        const response = await fetch(`${API_URL}/schedules`, {
            method: "POST",
            headers: {
                "Content-Type": "application/json",
                "Authorization": "Bearer " + token
            },
            body: JSON.stringify(body)
        });

        if (!response.ok) {
            const errorText = await response.text();
            throw new Error(`Erro ${response.status}: ${errorText}`);
        }

        alert("Agendamento criado com sucesso!");
        nameInput.value = "";
        emailInput.value = "";
        cancelForm();
        loadDay();
    } catch (error) {
        console.error("Erro ao criar agendamento:", error);
        alert("Erro ao criar agendamento: " + error.message);
    }
}

// =======================
// CANCEL SCHEDULE
// =======================
async function cancelSchedule(id) {
    if (!confirm("Cancelar este agendamento?")) return;
    const token = getToken();
    if (!token) {
        alert("Token não encontrado. Faça login novamente.");
        return;
    }

    try {
        const response = await fetch(`${API_URL}/appointments/${id}`, {
            method: "DELETE",
            headers: { "Authorization": "Bearer " + token }
        });

        if (!response.ok) {
            const errorText = await response.text();
            throw new Error(`Erro ${response.status}: ${errorText}`);
        }

        alert("Agendamento cancelado com sucesso!");
        loadDay();
    } catch (error) {
        console.error("Erro ao cancelar:", error);
        alert("Erro ao cancelar: " + error.message);
    }
}

// =======================
// EDIT MODAL
// =======================
async function loadEditServices() {
    const companyId = getCompanyId();
    const token = getToken();
    if (!companyId || !token) return;

    try {
        const response = await fetch(`${API_URL}/companies/${companyId}/services`, {
            headers: { "Authorization": "Bearer " + token }
        });
        if (!response.ok) throw new Error(`Erro ${response.status}`);
        const data = await response.json();
        const select = document.getElementById("edit-services");
        if (!select) return;
        select.innerHTML = "";
        if (!Array.isArray(data) || data.length === 0) {
            select.innerHTML = '<option disabled>Nenhum serviço</option>';
            return;
        }
        data.forEach(s => {
            const opt = document.createElement("option");
            opt.value = s.id;
            opt.textContent = `${s.name} (${s.durationMinutes} min)`;
            select.appendChild(opt);
        });
    } catch (error) {
        console.error("Erro ao carregar serviços para edição:", error);
    }
}

async function loadEditEmployees() {
    const companyId = getCompanyId();
    const token = getToken();
    if (!companyId || !token) return;

    try {
        const response = await fetch(`${API_URL}/employees`, {
            headers: { "Authorization": "Bearer " + token }
        });
        if (!response.ok) throw new Error(`Erro ${response.status}`);
        const data = await response.json();
        const select = document.getElementById("edit-employees");
        if (!select) return;
        select.innerHTML = "";
        if (!Array.isArray(data) || data.length === 0) {
            select.innerHTML = '<option disabled>Nenhum funcionário</option>';
            return;
        }
        data.forEach(e => {
            const opt = document.createElement("option");
            opt.value = e.id;
            opt.textContent = e.name + (e.specialty ? ` (${e.specialty})` : "");
            select.appendChild(opt);
        });
    } catch (error) {
        console.error("Erro ao carregar funcionários para edição:", error);
    }
}

async function openEditModal(id) {
    const token = getToken();
    if (!token) {
        alert("Token não encontrado. Faça login.");
        return;
    }

    try {
        const companyId = getCompanyId();
        const response = await fetch(`${API_URL}/companies/${companyId}/schedules?_t=${Date.now()}`, {
            headers: { "Authorization": "Bearer " + token }
        });
        if (!response.ok) throw new Error("Erro ao buscar dados");
        const data = await response.json();
        const appointment = data.find(a => a.id === id);
        if (!appointment) {
            alert("Agendamento não encontrado.");
            return;
        }

        document.getElementById("edit-id").value = id;
        document.getElementById("edit-services").value = appointment.serviceId || '';
        document.getElementById("edit-employees").value = appointment.employeeId || '';
        document.getElementById("edit-clientName").value = appointment.clientName || '';
        // O backend agora retorna clientEmail (não clientPhone)
        document.getElementById("edit-clientEmail").value = appointment.clientEmail || '';
        if (appointment.startTime) {
            const dt = new Date(appointment.startTime);
            const local = dt.toISOString().slice(0, 16);
            document.getElementById("edit-startTime").value = local;
        }

        document.getElementById("edit-modal").style.display = "flex";
        document.getElementById("edit-title").textContent = "Editar Agendamento";
    } catch (error) {
        console.error("Erro ao abrir edição:", error);
        alert("Erro ao carregar dados para edição: " + error.message);
    }
}

function closeEditModal() {
    document.getElementById("edit-modal").style.display = "none";
    document.getElementById("edit-id").value = "";
    document.getElementById("edit-clientName").value = "";
    document.getElementById("edit-clientEmail").value = "";
    document.getElementById("edit-startTime").value = "";
}

async function updateSchedule() {
    const id = document.getElementById("edit-id").value;
    if (!id) {
        alert("ID do agendamento não encontrado.");
        return;
    }

    const serviceId = document.getElementById("edit-services").value;
    const employeeId = document.getElementById("edit-employees").value;
    const clientName = document.getElementById("edit-clientName").value.trim();
    const clientEmail = document.getElementById("edit-clientEmail").value.trim();
    const startTime = document.getElementById("edit-startTime").value;

    if (!serviceId || !employeeId || !clientName || !clientEmail || !startTime) {
        alert("Preencha todos os campos.");
        return;
    }

    const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    if (!emailRegex.test(clientEmail)) {
        alert("Por favor, insira um e-mail válido.");
        return;
    }

    const token = getToken();
    if (!token) {
        alert("Token não encontrado. Faça login.");
        return;
    }

    const body = {
        serviceId: serviceId,
        employeeId: employeeId,
        clientName: clientName,
        clientEmail: clientEmail,   // <-- mudança aqui
        startTime: startTime
    };

    try {
        const response = await fetch(`${API_URL}/appointments/${id}`, {
            method: "PUT",
            headers: {
                "Content-Type": "application/json",
                "Authorization": "Bearer " + token
            },
            body: JSON.stringify(body)
        });

        if (!response.ok) {
            const errorText = await response.text();
            throw new Error(`Erro ${response.status}: ${errorText}`);
        }

        alert("Agendamento atualizado com sucesso!");
        closeEditModal();
        loadDay();
    } catch (error) {
        console.error("Erro ao atualizar:", error);
        alert("Erro ao atualizar: " + error.message);
    }
}