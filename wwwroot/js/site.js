function openModal(id){ const el=document.getElementById(id); if(el) el.style.display='flex'; }
function closeModal(id){ const el=document.getElementById(id); if(el) el.style.display='none'; }
document.addEventListener('click', e=>{ if(e.target.classList.contains('modal-overlay')) e.target.style.display='none'; });
