// All sound resources are original syntheses. No movie soundtrack or actor recordings.
export class AudioKit {
  constructor(session, theme, post) {
    this.session=session; this.settings=session.audio||{}; this.theme=theme; this.post=post;
    this.once=new Set(); this.buffers=new Map(); this.active=new Set(); this.voices=new Map(); this.disposed=false;
    try { this.context=new (window.AudioContext||window.webkitAudioContext)(); this.context.resume().catch(()=>{}); } catch { this.context=null; }
    this.nextCheck=0; this.events=[];this.voiceQueue=Promise.resolve();
    window.addEventListener('pagehide',()=>this.dispose(),{once:true});
  }
  async buffer(url) {
    if(!this.context) return null;
    if(!this.buffers.has(url)) this.buffers.set(url,fetch(url).then(r=>{if(!r.ok) throw Error('missing audio'); return r.arrayBuffer();}).then(b=>this.context.decodeAudioData(b)).catch(()=>null));
    return this.buffers.get(url);
  }
  async preload() {
    // Decode only; no source is started and no voice is spoken while confirmation is open.
    if(this.disposed||this.settings.muted||!this.settings.soundEnabled||this.theme.audioEnabled===false)return;
    const urls=Object.entries(this.theme.audioFiles||{}).map(([key,url])=>this.settings.audioFiles?.[key]?`https://audio.550w.local/sfx/${encodeURIComponent(key)}`:url);
    await Promise.all(urls.map(url=>this.buffer(url)));
  }
  async play(key, identity=key, gain=1) {
    if(this.once.has(identity)) return; this.once.add(identity);
    if(this.disposed||this.settings.muted||!this.settings.soundEnabled||this.theme.audioEnabled===false) return;
    const override=this.settings.audioFiles?.[key];
    const url=override?`https://audio.550w.local/sfx/${encodeURIComponent(key)}`:this.theme.audioFiles?.[key];
    if(!url) return;
    try {
      const volume=Math.max(0,Math.min(1,(this.settings.soundVolume??70)/100))*gain;
      const buffer=await this.buffer(url); if(this.disposed) return;
      this.events.push({key,identity,at:performance.now()});
      if(buffer&&this.context) {
        await this.context.resume(); if(this.disposed) return;
        const source=this.context.createBufferSource(),level=this.context.createGain(); source.buffer=buffer; level.gain.value=volume;
        source.connect(level); level.connect(this.context.destination); this.active.add(source);
        source.onended=()=>{this.active.delete(source);source.disconnect();level.disconnect();};
        let at=this.context.currentTime;
        if(key==='CHECK_OK'){at=Math.max(at,this.nextCheck);this.nextCheck=at+.055;}
        source.start(at);
      } else {
        const media=new Audio(url); media.volume=volume; this.active.add(media); media.onended=()=>this.active.delete(media); media.onerror=()=>this.active.delete(media); await media.play().catch(()=>this.active.delete(media));
      }
    } catch { /* Missing/unsupported files and blocked audio are always silent fallbacks. */ }
  }
  speak(key) {
    const identity='voice:'+key; if(this.once.has(identity)) return;this.once.add(identity);
    const enabled=this.session.kind==='boot'?this.settings.bootVoice:this.settings.shutdownVoice;
    if(this.disposed||this.settings.muted||!this.settings.voiceEnabled||!enabled||this.theme.voiceEnabled===false) return;
    const queued=this.voiceQueue.then(()=>this.speakNow(key)).catch(()=>{});this.voiceQueue=queued;return queued;
  }
  async speakNow(key) {
    if(this.disposed)return;
    const override=this.settings.voiceFiles?.[key];
    const voiceUrl=override?`https://audio.550w.local/voice/${encodeURIComponent(key)}`:this.theme.voiceProfile?.files?.[key];
    if(voiceUrl) {
      await new Promise(resolve=>{
        const media=new Audio(voiceUrl); media.volume=Math.max(0,Math.min(1,(this.settings.voiceVolume??60)/100));this.active.add(media);
        const done=()=>{this.active.delete(media);media.pause();resolve();}; media.onended=done;media.onerror=done;media.play().catch(done);setTimeout(done,3500);
      });return;
    }
    const phrase=(this.settings.phrases?.[key]||'').replaceAll('{app}',this.session.displayName).replaceAll('{system}',this.session.settings.systemLabel);
    if(!phrase) return;
    if(window.chrome?.webview) {
      const id=key+'-'+Math.round(performance.now());
      const url=await new Promise(resolve=>{this.voices.set(id,resolve);this.post({type:'VOICE_REQUEST',id,key});setTimeout(()=>this.voiceDone(id),3000);});
      if(url&&!this.disposed)await new Promise(resolve=>{const media=new Audio(url);media.volume=Math.max(0,Math.min(1,(this.settings.voiceVolume??60)/100));this.active.add(media);const done=()=>{this.active.delete(media);media.pause();resolve();};media.onended=done;media.onerror=done;media.play().then(()=>this.post({type:'VOICE_PLAYING',key})).catch(done);setTimeout(done,3500);});
    } else if(window.speechSynthesis) {
      await new Promise(resolve=>{try{const utterance=new SpeechSynthesisUtterance(phrase);utterance.lang='zh-CN';utterance.rate=.9;utterance.pitch=.72;utterance.volume=(this.settings.voiceVolume??60)/100;utterance.onend=resolve;utterance.onerror=resolve;window.speechSynthesis.speak(utterance);setTimeout(resolve,3000);}catch{resolve();}});
    }
  }
  voiceDone(id,url){const resolve=this.voices.get(id);if(resolve){this.voices.delete(id);resolve(url);}}
  dispose(){if(this.disposed)return;this.disposed=true;for(const item of this.active)try{if(item.stop)item.stop();else item.pause();}catch{}this.active.clear();for(const id of this.voices.keys())this.voiceDone(id);if(!window.chrome?.webview)try{window.speechSynthesis?.cancel();}catch{}try{this.context?.close().catch(()=>{});}catch{}this.buffers.clear();}
}
