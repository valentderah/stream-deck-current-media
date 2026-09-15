(function () {
	"use strict";

	var websocket = null;
	let pluginUUID = null;
	var actionUUID = null;
	var settings = {};
	var translations = {};

	function getDefaults() {
		var defaults = {};
		document.querySelectorAll("[data-setting][data-default]").forEach(function (el) {
			var key = el.getAttribute("data-setting");
			var value = el.getAttribute("data-default");
			if (el.type === "checkbox") {
				defaults[key] = value === "true";
			} else if (el.type === "range") {
				defaults[key] = parseInt(value, 10);
			} else {
				defaults[key] = value;
			}
		});
		defaults.idleImage = "";
		defaults.idleImageName = "";
		return defaults;
	}

	window.connectElgatoStreamDeckSocket = function (port, uuid, event, info, actionInfo) {
		pluginUUID = uuid;

		const parsedInfo = JSON.parse(info);
		const parsedActionInfo = JSON.parse(actionInfo);
		actionUUID = parsedActionInfo.action;
		const lang = parsedInfo.application.language || "en";

		settings = Object.assign({}, getDefaults(), parsedActionInfo.payload.settings || {});

		loadLocalization(lang).then(function (loaded) {
			translations = loaded;
			applyLocalization(translations);
			applySettings();
			document.body.style.visibility = "visible";
		});

		websocket = new WebSocket("ws://localhost:" + port);
		websocket.onopen = function () {
			websocket.send(JSON.stringify({
				event: event,
				uuid: uuid
			}));
		};
		websocket.onmessage = function (evt) {
			var data = JSON.parse(evt.data);
			if (data.event === "didReceiveSettings" && data.payload) {
				settings = Object.assign({}, getDefaults(), data.payload.settings || {});
				applySettings();
			}
		};

		bindSettingListeners();
		bindIdleImageControls();
	};

	function loadLocalization(lang) {
		return fetch("../" + lang + ".json")
			.then(function (res) {
				if (!res.ok) throw new Error(res.status);
				return res.json();
			})
			.then(function (data) {
				if (data.Localization) return data.Localization;
				throw new Error("No Localization key");
			})
			.catch(function () {
				if (lang === "en") return {};
				return fetch("../en.json")
					.then(function (res) { return res.json(); })
					.then(function (data) { return data.Localization || {}; })
					.catch(function () { return {}; });
			});
	}

	function applyLocalization(translations) {
		document.querySelectorAll("[data-i18n]").forEach(function (el) {
			var key = el.getAttribute("data-i18n");
			if (!translations[key]) return;
			if (el.id === "idleImageName" && settings.idleImageName) return;
			el.textContent = translations[key];
		});
	}

	function applySettings() {
		document.querySelectorAll("[data-setting]").forEach(function (el) {
			var key = el.getAttribute("data-setting");
			var value = settings[key];
			if (value === undefined) return;

			if (el.type === "checkbox") {
				el.checked = value === true || value === "true";
			} else {
				el.value = value;
			}

			if (el.type === "range") {
				var label = document.querySelector('.sdpi-range-value[data-for="' + key + '"]');
				if (label) label.textContent = value;
			}
		});

		var nameEl = document.getElementById("idleImageName");
		if (nameEl) {
			if (settings.idleImageName) {
				nameEl.textContent = settings.idleImageName;
			} else if (translations.NoFile) {
				nameEl.textContent = translations.NoFile;
			}
		}
	}

	function bindSettingListeners() {
		document.querySelectorAll("[data-setting]").forEach(function (el) {
			var eventType = el.type === "range" ? "input" : "change";
			el.addEventListener(eventType, function () {
				var key = el.getAttribute("data-setting");
				var val = el.type === "checkbox" ? el.checked : (el.type === "range" ? parseInt(el.value, 10) : el.value);
				settings[key] = val;

				if (el.type === "range") {
					var label = document.querySelector('.sdpi-range-value[data-for="' + key + '"]');
					if (label) label.textContent = val;
				}

				sendSettings();
			});
		});
	}

	function bindIdleImageControls() {
		var chooseBtn = document.getElementById("idleImageChoose");
		var clearBtn = document.getElementById("idleImageClear");

		chooseBtn.addEventListener("click", function () {
			sendToPlugin({ event: "pickIdleImage" });
		});

		clearBtn.addEventListener("click", function () {
			settings.idleImage = "";
			settings.idleImageName = "";
			applySettings();
			sendSettings();
		});
	}

	function sendToPlugin(payload) {
		if (!websocket || websocket.readyState !== WebSocket.OPEN) return;
		websocket.send(JSON.stringify({
			event: "sendToPlugin",
			action: actionUUID,
			context: pluginUUID,
			payload: payload
		}));
	}

	function sendSettings() {
		if (!websocket || websocket.readyState !== WebSocket.OPEN) return;
		websocket.send(JSON.stringify({
			event: "setSettings",
			context: pluginUUID,
			payload: settings
		}));
	}
})();
