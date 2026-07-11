const THEME_KEY = "rr-theme";

function getPreferredTheme() {
	const storedTheme = localStorage.getItem(THEME_KEY);
	if (storedTheme === "dark" || storedTheme === "light") {
		return storedTheme;
	}

	return window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light";
}

function applyTheme(theme) {
	document.documentElement.setAttribute("data-theme", theme);

	const isDark = theme === "dark";
	const navToggle = document.getElementById("theme-toggle-nav");
	if (navToggle) {
		// Show the icon for the mode you'd switch TO: sun when dark, moon when light.
		navToggle.textContent = isDark ? "☀️" : "🌙";
		const label = isDark ? "Switch to light mode" : "Switch to dark mode";
		navToggle.setAttribute("aria-label", label);
		navToggle.setAttribute("title", label);
		navToggle.setAttribute("aria-pressed", String(isDark));
	}

	const settingsToggle = document.getElementById("dark-mode-toggle");
	if (settingsToggle) {
		settingsToggle.checked = isDark;
	}
}

function setTheme(theme) {
	localStorage.setItem(THEME_KEY, theme);
	applyTheme(theme);
}

document.addEventListener("DOMContentLoaded", () => {
	applyTheme(getPreferredTheme());

	const navToggle = document.getElementById("theme-toggle-nav");
	if (navToggle) {
		navToggle.addEventListener("click", () => {
			const current = document.documentElement.getAttribute("data-theme") === "dark" ? "dark" : "light";
			setTheme(current === "dark" ? "light" : "dark");
		});
	}

	const settingsToggle = document.getElementById("dark-mode-toggle");
	if (settingsToggle) {
		settingsToggle.addEventListener("change", () => {
			setTheme(settingsToggle.checked ? "dark" : "light");
		});
	}

	// Show any server-rendered flash toasts (Bootstrap), then auto-dismiss.
	if (window.bootstrap && bootstrap.Toast) {
		document.querySelectorAll(".toast.app-toast").forEach((el) => {
			bootstrap.Toast.getOrCreateInstance(el, { autohide: true }).show();
		});
	}
});
