document.addEventListener("click", (event) => {
  const button = event.target.closest(".details-toggle");

  if (!button) {
    return;
  }

  const target = document.getElementById(button.dataset.target);

  if (target) {
    target.hidden = !target.hidden;
  }
});
