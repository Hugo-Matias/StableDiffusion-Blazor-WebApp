export function init(
	instance,
	container,
	canvasDraw,
	canvasPointer,
	canvasWork,
	imgCanvas,
	imgWork
) {
	function getContext(id) {
		return document.getElementById(id).getContext("2d");
	}

	function getData(ctx) {
		return ctx.getImageData(0, 0, canvasDrawElem.width, canvasDrawElem.height);
	}

	function saveImageData() {
		const data = canvasWorkElem.toDataURL("image/png");
		imgWork.src = data;
	}

	function saveMask() {
		const isCanvasEmpty = convertMaskTransparency();
		if (!isCanvasEmpty) {
			saveImageData();
			dotnetObject.invokeMethodAsync(
				"LoadImageData",
				imgCanvas.src,
				imgWork.src
			);
		}
	}

	function convertMaskTransparency() {
		const ctxDraw = getContext(canvasDraw.additionalAttributes.id);
		const ctxWork = getContext(canvasWork.additionalAttributes.id);
		const drawData = getData(ctxDraw);
		const isCanvasEmpty = !drawData.data.some((color) => color !== 0);
		if (isCanvasEmpty) return true;
		let workData = drawData;
		for (let i = 0; i < workData.data.length; i += 4) {
			// If the current pixel alpha (i+3) is less than fully opaque turn the pixel black, otherwise it stays white, i.e mask.
			if (workData.data[i + 3] == 0) {
				workData.data[i] = 0; //red
				workData.data[i + 1] = 0; // green
				workData.data[i + 2] = 0; // blue
				workData.data[i + 3] = 255; // alpha
			} else if (workData.data[i + 3] <= 255) {
				workData.data[i] = 255; //red
				workData.data[i + 1] = 255; // green
				workData.data[i + 2] = 255; // blue
				workData.data[i + 3] = 255; // alpha
			}
		}
		ctxWork.putImageData(workData, 0, 0);
		return false;
	}

	function setInpaint() {
		convertInpaintPixels();
		saveImageData();
		imgCanvas.src = imgWork.src;
	}

	function saveInpaint() {
		convertInpaintPixels();
		saveImageData();
		dotnetObject.invokeMethodAsync("LoadImageData", imgWork.src, null);
	}

	function convertInpaintPixels() {
		const ctxDraw = getContext(canvasDraw.additionalAttributes.id);
		const ctxWork = getContext(canvasWork.additionalAttributes.id);
		ctxWork.drawImage(imgCanvas, 0, 0);
		const drawData = getData(ctxDraw);
		const workData = getData(ctxWork);

		for (let i = 0; i < workData.data.length; i += 4) {
			// If the current pixel isn't transparent (drawing done on canvas) copy the pixel to the work canvas
			if (drawData.data[i + 3] == 255) {
				workData.data[i] = drawData.data[i];
				workData.data[i + 1] = drawData.data[i + 1];
				workData.data[i + 2] = drawData.data[i + 2];
				workData.data[i + 3] = drawData.data[i + 3];
			}
		}
		ctxWork.putImageData(workData, 0, 0);
	}

	function clearData() {
		const ctx = getContext(canvasDraw.additionalAttributes.id);
		let data = getData(ctx);

		for (let i = 0; i < data.data.length; i++) {
			data.data[i] = 0;
		}
		ctx.putImageData(data, 0, 0);
	}

	function toggleCanvas(isEnabled) {
		if (isEnabled) {
			canvasDrawElem.classList.remove("disable");
			canvasPointerElem.classList.remove("disable");
			canvasWorkElem.classList.remove("disable");
		} else {
			canvasDrawElem.classList.add("disable");
			canvasPointerElem.classList.add("disable");
			canvasWorkElem.classList.add("disable");
		}
	}

	function resizeCanvas() {
		let w, h, multX, multY;
		if (!imgCanvas.src.includes("/no_image.png")) {
			w = imgCanvas.naturalWidth;
			h = imgCanvas.naturalHeight;
			if (container.clientWidth > imgCanvas.clientWidth) {
				container.setAttribute("style", `width:${imgCanvas.clientWidth}px;`);
			}
			else if (container.clientHeight > imgCanvas.clientHeight) {
				container.setAttribute("style", `height:${imgCanvas.clientHeight}px;`);
			}
			multX = w / container.clientWidth;
			multY = h / container.clientHeight;
			toggleCanvas(true);
		} else {
			w = container.clientWidth;
			h = container.clientHeight;
			multX = multY = 1;
			toggleCanvas(false);
		}

		dotnetObject.invokeMethodAsync("OnResize", w, h, container.clientWidth, container.clientHeight, multX, multY);
	}

	function loadImage(data) {
		imgCanvas.onload = () => {
			resizeCanvas();
		};
		imgCanvas.src = data;
	}

	function deleteImage(imgSource) {
		imgCanvas.src = imgSource;
	}

	window.dotnetObject = instance;
	const canvasDrawElem = document.getElementById(
		canvasDraw.additionalAttributes.id
	);
	const canvasPointerElem = document.getElementById(
		canvasPointer.additionalAttributes.id
	);
	const canvasWorkElem = document.getElementById(
		canvasWork.additionalAttributes.id
	);

	window.addEventListener("resize", resizeCanvas);

	resizeCanvas();

	return {
		loadImage,
		setInpaint,
		deleteImage,
		saveMask,
		saveInpaint,
		clearData,
		dispose: () => {
			window.removeEventListener("resize", resizeCanvas);
		},
	};
}
