(() => {
const form = document.querySelector('#assistant-form'), input = document.querySelector('#assistant-input'), messages = document.querySelector('#assistant-messages'), send = document.querySelector('#assistant-send');
if (!form) return;
const token = document.querySelector('#assistant-csrf input').value;
function append(text, role) { const row = document.createElement('div'); row.className = `chat-message chat-${role}`; const bubble = document.createElement('div'); bubble.className = 'chat-bubble'; bubble.textContent = text; row.append(bubble); messages.append(row); messages.scrollTop = messages.scrollHeight; return bubble; }
form.addEventListener('submit', async e => {
 e.preventDefault(); const message = input.value.trim(); if (!message || send.disabled) return;
 append(message, 'user'); input.value = ''; send.disabled = true; input.disabled = true; send.textContent = 'Đang trả lời…'; form.setAttribute('aria-busy', 'true');
 const pending = append('Đang tìm câu trả lời…', 'assistant'); const controller = new AbortController(); const timeout = setTimeout(() => controller.abort(), 55000);
 try {
  const response = await fetch(form.closest('.assistant-workspace').dataset.endpoint, {method:'POST',headers:{'Content-Type':'application/json','RequestVerificationToken':token},body:JSON.stringify({message}),signal:controller.signal});
  if (response.redirected || response.status === 401) throw new Error('Phiên đăng nhập đã hết hạn. Hãy đăng nhập lại.');
  if (response.status === 429) throw new Error('Bạn gửi câu hỏi quá nhanh. Vui lòng thử lại sau một phút.');
  const data = await response.json(); if (!response.ok) throw new Error(data.reply || 'Không gửi được câu hỏi.');
  pending.textContent = data.reply;
  const label = document.createElement('span'); label.className = 'reply-mode'; label.textContent = data.mode === 'ai' ? 'AI · Gemini' : 'Tra cứu kho hướng dẫn'; pending.prepend(label);
  document.querySelector('#assistant-mode').textContent = data.notice || (data.mode === 'ai' ? 'Đang trả lời bằng AI · Gemini' : 'Đang tra cứu kho hướng dẫn');
  const sources = document.querySelector('#assistant-sources'); sources.replaceChildren();
  for (const source of data.sources || []) { const link = document.createElement('a'); link.className='source-link'; link.href=source.url; link.textContent='▧ '+source.title; sources.append(link); }
  if (!sources.childElementCount) { const p=document.createElement('p');p.textContent=data.mode === 'ai' ? 'Câu trả lời từ Gemini, không sử dụng bài hướng dẫn.' : 'Hiện chưa có hướng dẫn liên quan để trả lời thay thế.';sources.append(p); }
 } catch (error) { pending.textContent = error.name === 'AbortError' ? 'Phản hồi quá lâu. Hãy thử lại sau hoặc chuyển nhân viên.' : error.message || 'Không thể kết nối. Vui lòng thử lại.'; input.value=message; }
 finally { clearTimeout(timeout); send.disabled=false;input.disabled=false;send.textContent='Gửi ↑';form.removeAttribute('aria-busy');input.focus();messages.scrollTop=messages.scrollHeight; }
});
input.addEventListener('keydown', e => {if(e.key==='Enter' && !e.shiftKey && !e.isComposing){e.preventDefault();form.requestSubmit();}});
document.querySelectorAll('[data-prompt]').forEach(button=>button.addEventListener('click',()=>{if(send.disabled)return;input.value=button.dataset.prompt;form.requestSubmit();}));
messages.scrollTop=messages.scrollHeight;
})();
