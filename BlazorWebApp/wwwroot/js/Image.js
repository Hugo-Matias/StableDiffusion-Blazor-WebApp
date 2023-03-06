export function init(instance) {
	function loaded() {
		dotnetObject.invokeMethodAsync("HandleImageLoaded", true);
	}

	window.dotnetObject = instance;
	let img = document.getElementById("image");

	if (img.complete) {
		loaded();
	} else {
		img.addEventListener("load", loaded);
	}

	return {
		dispose: () => {
			img.removeEventListener("load", loaded);
		},
	};
}
