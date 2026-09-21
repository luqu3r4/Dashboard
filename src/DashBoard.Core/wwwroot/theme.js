window.dashboardTheme = {
    toggle: function () {
        var next = document.documentElement.getAttribute("data-bs-theme") === "dark" ? "light" : "dark";
        document.documentElement.setAttribute("data-bs-theme", next);
        document.cookie = "dashboard-theme=" + next + ";path=/;max-age=31536000;samesite=lax";
    }
};
