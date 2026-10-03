"""Generate the original controller icon and eleven event-synchronized PCM sounds.
No sampled audio, movie assets, external sound library or copyrighted voice is used.
Requires Python 3 and Pillow for the icon. WAV generation uses only the standard library.
"""
import pathlib, math, random, wave, struct
from PIL import Image, ImageDraw, ImageFont
root=pathlib.Path(__file__).resolve().parents[1]
assets=root/'assets'; assets.mkdir(exist_ok=True)
im=Image.new('RGBA',(256,256),(13,15,18,255)); d=ImageDraw.Draw(im)
d.rounded_rectangle((10,10,246,246),radius=28,outline=(232,160,32),width=8)
d.rectangle((33,42,223,55),fill=(232,160,32));d.polygon([(33,72),(57,72),(73,94),(49,94)],fill=(255,192,67));d.line((83,82,216,82),fill=(97,74,36),width=3)
font_path=pathlib.Path('C:/Windows/Fonts/consolab.ttf')
font=ImageFont.truetype(str(font_path),57) if font_path.exists() else ImageFont.load_default()
d.text((128,129),'550W',font=font,anchor='mm',fill=(255,192,67));d.line((35,177,221,177),fill=(97,74,36),width=3)
for x in range(35,221,17):d.rectangle((x,194,x+10,207),fill=(232,160,32))
im.save(assets/'controller.ico',sizes=[(16,16),(24,24),(32,32),(48,48),(64,64),(128,128),(256,256)])
audio=root/'Themes/550W/audio'; audio.mkdir(parents=True,exist_ok=True)
rate=44100
def env(t,length,attack=.01):return min(1,t/attack)*max(0,1-t/length)**1.4
def tone(t,f):return math.sin(2*math.pi*f*t)
def write(name,length,fn):
 random.seed(550)
 values=[max(-.85,min(.85,fn(i/rate,length)*env(i/rate,length))) for i in range(int(rate*length))]
 with wave.open(str(audio/(name+'.wav')),'wb') as out:
  out.setnchannels(1);out.setsampwidth(2);out.setframerate(rate);out.writeframes(b''.join(struct.pack('<h',int(v*32767)) for v in values))
write('power_on',.75,lambda t,L:.34*tone(t,48+26*t)+.13*tone(t,96)+.025*(random.random()*2-1))
write('core_expand',.75,lambda t,L:.17*math.sin(2*math.pi*(72*t+105*t*t))+.06*tone(t,510+150*t)+.014*(random.random()*2-1))
write('scan',.32,lambda t,L:.13*math.sin(2*math.pi*(640*t+460*t*t))+.022*(random.random()*2-1))
write('check_ok',.085,lambda t,L:.13*tone(t,880)+.035*tone(t,1320))
write('process_detected',.23,lambda t,L:.14*tone(t,440 if t<.1 else 660)+.035*tone(t,1320))
write('ready',.68,lambda t,L:.17*tone(t,220)+.1*tone(t,440 if t<.25 else 550)+.06*tone(t,660 if t<.42 else 880))
write('unlock',.2,lambda t,L:.11*math.sin(2*math.pi*(620*t-620*t*t))+.06*tone(t,110))
write('shutdown_begin',.75,lambda t,L:.3*math.sin(2*math.pi*(95*t-38*t*t))+.055*tone(t,190)*(1-t/L))
write('relay_off',.095,lambda t,L:.11*(random.random()*2-1)*math.exp(-t*60)+.13*tone(t,160)*math.exp(-t*28))
write('crt_collapse',.4,lambda t,L:.09*math.sin(2*math.pi*(840*t-780*t*t))+.09*math.sin(2*math.pi*(90*t-55*t*t)))
write('power_off',.42,lambda t,L:.24*math.sin(2*math.pi*(55*t-25*t*t))+.03*(random.random()*2-1)*math.exp(-t*20))
(audio/'LICENSE.txt').write_text('Original synthesized audio created for 550W AI Controller.\nCopyright (c) 2026 550W AI Controller contributors.\nLicensed under the MIT License in the distribution root.\nNo film sound samples or actor recordings are included.\n',encoding='utf-8')
print('Generated original icon and eleven PCM WAV files.')
