const API_URL = "http://localhost:5182";

function getToken() {
    return localStorage.getItem("token");
}

async function loadCompanies() {
    const response = await fetch(`${API_URL}/companies`, {
        headers: {
            "Authorization": "Bearer " + getToken()
        }
    });

    const data = await response.json();
    const list = document.getElementById("companies");
    list.innerHTML = "";

    data.forEach(c => {
        const li = document.createElement("li");
        li.innerHTML = `
            ${c.name}
            <button onclick="selectCompany('${c.id}')">Entrar</button>
        `;
        list.appendChild(li);
    });
}

function selectCompany(companyId) {
    localStorage.setItem("companyId", companyId);
    window.location.href = "schedule.html";
}

async function createCompany() {
    const name = document.getElementById("companyName").value;

    const response = await fetch(`${API_URL}/companies`, {
        method: "POST",
        headers: {
            "Content-Type": "application/json",
            "Authorization": "Bearer " + getToken()
        },
        body: JSON.stringify({ name })
    });

    document.getElementById("companyName").value = "";
    loadCompanies();
}
