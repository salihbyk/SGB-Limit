using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TokenTray;

internal static class Program
{
    static string source = "", output = "", plate = "";
    static readonly Brush White = new SolidColorBrush(Color.FromRgb(238,243,240));
    static readonly Brush Muted = new SolidColorBrush(Color.FromRgb(167,187,179));
    static readonly Brush Sage = new SolidColorBrush(Color.FromRgb(164,220,189));
    const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static BitmapSource Load(string path) { var b = new BitmapImage(); b.BeginInit(); b.CacheOption = BitmapCacheOption.OnLoad; b.UriSource = new Uri(Path.GetFullPath(path)); b.EndInit(); b.Freeze(); return b; }
    static void Text(DrawingContext dc,string text,double x,double y,double size,Brush brush,bool bold=false)
    { var f = new FormattedText(text,CultureInfo.GetCultureInfo("en-US"),FlowDirection.LeftToRight,new Typeface(new FontFamily("Segoe UI"),FontStyles.Normal,bold?FontWeights.SemiBold:FontWeights.Normal,FontStretches.Normal),size,brush,1); dc.DrawText(f,new Point(x,y)); }
    static void Image(DrawingContext dc,string name,double x,double y,double width)
    { var b=Load(Path.Combine(source,name+"-ui.png")); dc.DrawImage(b,new Rect(x,y,width,width*b.PixelHeight/b.PixelWidth)); }
    static void Save(string name,int width,int height,Action<DrawingContext> draw)
    {
        var v=new DrawingVisual(); using(var dc=v.RenderOpen()){
            dc.DrawRectangle(new LinearGradientBrush(Color.FromRgb(15,23,24),Color.FromRgb(28,41,36),45),null,new Rect(0,0,width,height)); draw(dc);
        }
        var b=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32); b.Render(v); var e=new PngBitmapEncoder();e.Frames.Add(BitmapFrame.Create(b));using var f=File.Create(Path.Combine(output,name+".png"));e.Save(f);
    }
    static void Laptop(DrawingContext dc,string desktop,double x,double y,double width)
    {
        var frame=Load(plate); var factor=width/frame.PixelWidth;
        dc.DrawImage(frame,new Rect(x,y,width,frame.PixelHeight*factor));
        // Native screenshot is uniformly fitted to the blank flat display. Thin pillarboxes
        // preserve the original UI proportions; the app is never repainted by an image model.
        var screen=new Rect(x+264*factor,y+72*factor,1144*factor,606*factor);
        dc.DrawRectangle(Brushes.Black,null,screen);
        var bitmap=Load(Path.Combine(source,desktop+"-desktop.png"));
        var scale=Math.Min(screen.Width/bitmap.PixelWidth,screen.Height/bitmap.PixelHeight);
        var target=new Rect(screen.Left+(screen.Width-bitmap.PixelWidth*scale)/2,screen.Top,bitmap.PixelWidth*scale,bitmap.PixelHeight*scale);
        dc.DrawImage(bitmap,target);
    }
    static BitmapSource Taskbar(string style,bool compact=false,bool names=true,bool remaining=false,int bars=5)
    {
        var assembly=typeof(Settings).Assembly;
        var now=DateTimeOffset.Now;
        var snapshots=assembly.GetType("TokenTray.MediaRenderer")!.GetMethod("Demo",Static)!.Invoke(null,[now,42d]);
        var rows=assembly.GetType("TokenTray.TaskbarReadings")!.GetMethod("FromSnapshots",Static)!.Invoke(null,[snapshots,remaining,now,TimeSpan.FromMinutes(2)]);
        var optionsType=assembly.GetType("TokenTray.WidgetOptions")!;
        var ctor=optionsType.GetConstructors().First(c=>c.GetParameters().Length>2);
        var values=ctor.GetParameters().Select(p=>p.HasDefaultValue?p.DefaultValue: p.ParameterType==typeof(bool)?(object)true:Enum.Parse(p.ParameterType,style)).ToArray();
        values[0]=Enum.Parse(ctor.GetParameters()[0].ParameterType,style);values[1]=names;values[5]=remaining;values[7]=bars;
        var options=ctor.Invoke(values);
        var palette=Activator.CreateInstance(assembly.GetType("TokenTray.Palette")!,[true]);
        var renderer=assembly.GetType("TokenTray.WidgetRenderer")!.GetMethod("Render",Static)!;
        var parameters=renderer.GetParameters();
        object?[] args=[rows,options,palette,4d,48d,false,0d,0,Enum.Parse(parameters[^1].ParameterType,compact?"Icons":"Full")];
        return (BitmapSource)renderer.Invoke(null,args)!;
    }
    static void Row(DrawingContext dc,string label,BitmapSource bitmap,double y)
    {
        Text(dc,label,70,y+39,30,White,true);
        var box=new Rect(355,y,1375,144);
        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(24,30,34)),new Pen(new SolidColorBrush(Color.FromRgb(48,62,60)),1),box,12,12);
        var scale=Math.Min(1325d/bitmap.PixelWidth,118d/bitmap.PixelHeight);
        dc.DrawImage(bitmap,new Rect(box.Left+24,box.Top+(box.Height-bitmap.PixelHeight*scale)/2,bitmap.PixelWidth*scale,bitmap.PixelHeight*scale));
    }
    [STAThread] static void Main(string[] args)
    {
        if(args.Length!=3)throw new ArgumentException("Usage: MediaGallery <native-capture-folder> <output-folder> <laptop-base.png>");
        source=Path.GetFullPath(args[0]);output=Path.GetFullPath(args[1]);plate=Path.GetFullPath(args[2]);Directory.CreateDirectory(output);
        Save("laptop-hero",2000,1350,dc=>{
            Text(dc,"SGB Limit",100,46,98,White,true);
            Text(dc,"AI usage limits on your Windows desktop.",108,169,35,Muted);
            Laptop(dc,"top-expanded",0,220,2000);
            Text(dc,"Screen-edge Dynamic Island",1340,248,25,Sage,true);
            dc.DrawLine(new Pen(Sage,2),new Point(1360,290),new Point(1150,316));dc.DrawEllipse(Sage,null,new Point(1150,316),5,5);
            Text(dc,"Windows x64   ·   Native WPF   ·   MIT",100,1295,25,Sage);
            Text(dc,"Sample quotas · device mockup",1450,1298,21,Muted);
        });
        Save("laptop-hero-tr",2000,1350,dc=>{
            Text(dc,"SGB Limit",100,46,98,White,true);
            Text(dc,"Yapay zekâ kotaların Windows masaüstünde.",108,169,35,Muted);
            Laptop(dc,"top-expanded",0,220,2000);
            Text(dc,"Ekran kenarındaki Dinamik Ada",1300,248,25,Sage,true);
            dc.DrawLine(new Pen(Sage,2),new Point(1360,290),new Point(1150,316));dc.DrawEllipse(Sage,null,new Point(1150,316),5,5);
            Text(dc,"Windows x64   ·   Yerel WPF   ·   MIT",100,1295,25,Sage);
            Text(dc,"Örnek kotalar · laptop mockup",1450,1298,21,Muted);
        });
        Save("dynamic-island-states",1800,1340,dc=>{
            Text(dc,"A real screen-edge Dynamic Island.",75,46,58,White,true);
            Text(dc,"Compact at rest. Expands when you hover.",80,133,29,Muted);
            Text(dc,"01  COMPACT",80,228,25,Sage,true);Text(dc,"02  EXPANDED",970,228,25,Sage,true);
            Laptop(dc,"top-compact",0,280,900);Laptop(dc,"top-expanded",900,280,900);
            Text(dc,"Native UI close-ups",80,821,24,Muted);
            Image(dc,"top-compact",80,939,580);Image(dc,"top-expanded",780,845,940);
            Text(dc,"Attached to the top edge · Preserves its anchor · 350 ms transition",80,1280,25,Sage);
        });
        var styles=new[]{("Identity","Icon + name"),("Rings","Rings"),("Ribbon","Ribbon"),("Dial","Dial"),("Signal","Signal")};
        Save("taskbar-styles",1800,1540,dc=>{
            Text(dc,"Five taskbar styles. Your choice.",70,45,58,White,true);
            Text(dc,"Same synthetic quotas, rendered by SGB Limit's native taskbar component.",75,134,27,Muted);
            for(var i=0;i<styles.Length;i++)Row(dc,styles[i].Item2,Taskbar(styles[i].Item1),225+i*208);
            Row(dc,"Compact",Taskbar("Signal",true),1265);
            Text(dc,"Signal: 3 or 5 bars · Used / remaining · Compact icons + percentage when space gets tight",75,1466,24,Sage);
        });
        Save("taskbar-variants",1800,1380,dc=>{
            Text(dc,"The same styles, without names.",70,45,58,White,true);
            Text(dc,"Every current taskbar style also supports a name-free presentation.",75,134,27,Muted);
            for(var i=0;i<styles.Length;i++)Row(dc,styles[i].Item2,Taskbar(styles[i].Item1,names:false),225+i*208);
            Text(dc,"Synthetic quotas · Native WPF renderer · Actual available styles",75,1310,24,Sage);
        });
        Save("signal-options",1800,1170,dc=>{
            Text(dc,"Signal bars follow the displayed quota.",70,45,57,White,true);
            Text(dc,"3 or 5 bars. Used or remaining. The same binding quota as the account panel.",75,134,27,Muted);
            Row(dc,"3 · used",Taskbar("Signal",bars:3),225);
            Row(dc,"5 · used",Taskbar("Signal",bars:5),435);
            Row(dc,"3 · left",Taskbar("Signal",remaining:true,bars:3),645);
            Row(dc,"5 · left",Taskbar("Signal",remaining:true,bars:5),855);
            Text(dc,"Synthetic quotas · Partial fills preserve small percentages",75,1096,24,Sage);
        });
        Save("floating-styles",1800,1040,dc=>{
            Text(dc,"Three floating gauges.",80,48,60,White,true);
            Text(dc,"A circular alternative to the taskbar and Dynamic Island.",85,139,29,Muted);
            var gauges=new[]{("floating-halo","Halo"),("floating-orbit","Orbit"),("floating-comet","Comet · idle")};
            for(var i=0;i<3;i++){Image(dc,gauges[i].Item1,150+i*540,350,410);Text(dc,gauges[i].Item2,250+i*540,825,31,White,true);}
            Text(dc,"Comet animates on hover when motion is enabled. Native WPF · synthetic quotas.",85,959,25,Sage);
        });
        var desktop=Load(Path.Combine(source,"taskbar-signal-desktop.png"));
        var cropped=new CroppedBitmap(desktop,new Int32Rect(Math.Max(0,desktop.PixelWidth-1360),desktop.PixelHeight-260,Math.Min(1360,desktop.PixelWidth),260));
        var cropEncoder=new PngBitmapEncoder();cropEncoder.Frames.Add(BitmapFrame.Create(cropped));
        using(var file=File.Create(Path.Combine(output,"taskbar-desktop.png")))cropEncoder.Save(file);
        File.WriteAllText(Path.Combine(output,"provenance.json"),JsonSerializer.Serialize(new {
            data="Synthetic quotas only. Native production renderers; UI values and text preserved.",
            device="Original unbranded laptop image generated using OpenAI ImageGen. Screen is a deterministic uniform fit of the native desktop capture, with narrow pillarboxes preserving proportions.",
            taskbar="Five currently selectable styles plus compact density rendered at 4x by production WidgetRenderer, not legacy hidden styles.",
            wallpaper="Original ImageGen wallpaper from the previous presentation. No upstream README screenshots copied."
        },new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine("Exported laptop hero, two Dynamic Island states, five taskbar styles + compact, three gauges and native desktop scene.");
    }
}
