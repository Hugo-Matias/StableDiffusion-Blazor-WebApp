window.clipboardCopy = {
	copyText: function (text) {
		navigator.clipboard.writeText(text)
			.catch(function (error) {
				alert(error);
			});
	}
};
