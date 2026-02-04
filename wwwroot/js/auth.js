// ========== AUTH.JS - Login Logic with Auto-End ==========

const API_URL = "http://localhost:5286";

async function login() {
    const usernameInput = document.getElementById("username");
    const passwordInput = document.getElementById("password");
    const error = document.getElementById("error");

    if (!usernameInput || !passwordInput) {
        console.error("Login inputs not found");
        return;
    }

    const username = usernameInput.value.trim();
    const password = passwordInput.value.trim();

    if (!username || !password) {
        error.innerText = "Please enter username and password";
        return;
    }

    error.innerText = "";

    try {
        const response = await fetch(`${API_URL}/api/auth/login`, {
            method: "POST",
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify({ username, password })
        });

        if (!response.ok) {
            error.innerText = "Invalid username or password";
            return;
        }

        const user = await response.json();
        
        console.log("Login successful, user data:", user);
        
        // Store user in localStorage
        localStorage.setItem("user", JSON.stringify(user));
        
        // Redirect based on role
        if (user.isAdmin === true) {
            console.log("Redirecting to admin page");
            window.location.href = "/admin.html";
        } else {
            console.log("Redirecting to worker page");
            window.location.href = "/worker.html";
        }

    } catch (err) {
        console.error("Login error:", err);
        error.innerText = "Cannot connect to server. Is the API running?";
    }
}

// Allow Enter key to submit
document.addEventListener("DOMContentLoaded", () => {
    const passwordInput = document.getElementById("password");
    if (passwordInput) {
        passwordInput.addEventListener("keypress", (e) => {
            if (e.key === "Enter") {
                login();
            }
        });
    }
});