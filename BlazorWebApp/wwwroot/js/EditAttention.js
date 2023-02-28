addEventListener("keydown", (event) => {
  let target = event.target;
  const positive = "ArrowUp";
  const negative = "ArrowDown";

  if (!event.ctrlKey || (event.key != positive && event.key != negative))
    return;
  if (!target.id || (target.id != "prompt" && target.id != "negativePrompt"))
    return;

  let selectionStart = target.selectionStart;
  let selectionEnd = target.selectionEnd;

  // If the user hasn't selected anything, let's select their current parenthesis block
  if (selectionStart === selectionEnd) {
    // Find opening parenthesis around current cursor
    const before = target.value.substring(0, selectionStart);
    let beforeParen = before.lastIndexOf("(");
    if (beforeParen == -1) return;
    let beforeParenClose = before.lastIndexOf(")");
    while (beforeParenClose !== -1 && beforeParenClose > beforeParen) {
      beforeParen = before.lastIndexOf("(", beforeParen - 1);
      beforeParenClose = before.lastIndexOf(")", beforeParenClose - 1);
    }

    // Find closing parenthesis around current cursor
    const after = target.value.substring(selectionStart);
    let afterParen = after.indexOf(")");
    if (afterParen == -1) return;
    let afterParenOpen = after.indexOf("(");
    while (afterParenOpen !== -1 && afterParen > afterParenOpen) {
      afterParen = after.indexOf(")", afterParen + 1);
      afterParenOpen = after.indexOf("(", afterParenOpen + 1);
    }
    if (beforeParen === -1 || afterParen === -1) return;

    // Set the selection to the text between the parenthesis
    const parenContent = target.value.substring(
      beforeParen + 1,
      selectionStart + afterParen
    );
    const lastColon = parenContent.lastIndexOf(":");
    selectionStart = beforeParen + 1;
    selectionEnd = selectionStart + lastColon;
    target.setSelectionRange(selectionStart, selectionEnd);
  }
  event.preventDefault();

  if (selectionStart == 0 || target.value[selectionStart - 1] != "(") {
    target.value =
      target.value.slice(0, selectionStart) +
      "(" +
      target.value.slice(selectionStart, selectionEnd) +
      ":1.0)" +
      target.value.slice(selectionEnd);

    target.focus();
    target.selectionStart = selectionStart + 1;
    target.selectionEnd = selectionEnd + 1;
  } else {
    end = target.value.slice(selectionEnd + 1).indexOf(")") + 1;
    weight = parseFloat(
      target.value.slice(selectionEnd + 1, selectionEnd + 1 + end)
    );
    if (isNaN(weight)) return;
    if (event.key == negative) weight -= 0.1;
    if (event.key == positive) weight += 0.1;

    weight = parseFloat(weight.toPrecision(12));

    target.value =
      target.value.slice(0, selectionEnd + 1) +
      weight +
      target.value.slice(selectionEnd + 1 + end - 1);

    target.focus();
    target.selectionStart = selectionStart;
    target.selectionEnd = selectionEnd;
  }
});
