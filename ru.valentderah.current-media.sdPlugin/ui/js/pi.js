(function () {
	"use strict";

	let websocket = null;
	let pluginUUID = null;
	let settings = {};

	function getDefaults() {
		var defaults = {};
		document.querySelectorAll("[data-setting][data-default]").forEach(function (el) {
			var key = el.getAttribute("data-setting");
			var value = el.getAttribute("data-default");
			defaults[key] = el.type === "checkbox" ? value === "true" : value;
		});
		return defaults;
	}

	window.connectElgatoStreamDeckSocket = function (port, uuid, event, info, actionInfo) {
		pluginUUID = uuid;

		const parsedInfo = JSON.parse(info);
		const parsedActionInfo = JSON.parse(actionInfo);
		const lang = parsedInfo.application.language || "en";

		settings = Object.assign({}, getDefaults(), parsedActionInfo.payload.settings || {});

		loadLocalization(lang).then(function (translations) {
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
			if (translations[key]) el.textContent = translations[key];
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
		});
	}

	function bindSettingListeners() {
		document.querySelectorAll("[data-setting]").forEach(function (el) {
			var eventType = el.type === "checkbox" ? "change" : "change";
			el.addEventListener(eventType, function () {
				var key = el.getAttribute("data-setting");
				settings[key] = el.type === "checkbox" ? el.checked : el.value;
				sendSettings();
			});
		});
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
