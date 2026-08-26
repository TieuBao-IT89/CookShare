// Các tương tác đơn giản; nội dung giao diện được khai báo trực tiếp trong index.html.
const menuButton = document.querySelector('.menu-btn');
const navLinks = document.querySelector('.nav-links');

menuButton?.addEventListener('click', () => navLinks.classList.toggle('open'));

document.querySelectorAll('.filter').forEach((button) => {
  button.addEventListener('click', () => {
    document.querySelectorAll('.filter').forEach((item) => item.classList.remove('active'));
    button.classList.add('active');
  });
});
