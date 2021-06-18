using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Vlc.DotNet.Core.Interops;
using Vlc.DotNet.Core.Interops.Signatures;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Path = System.IO.Path;
using System.Net.NetworkInformation;

namespace WpfVLC
{
    //vlc.exe test.mp4 -vvv --no-loop  --sout "#es{access=file, dst-video=e:/video_%d.%c, dst-audio=e:/audio_%d.%c}"
    //vlc.exe test.mp4 -vvv --sout "#duplicate{dst=standard{access=file,mux=avi,dst=e:/test.avi}, dst=rtp{dst=192.168.9.80,name=stream,sdp=rtsp://192.168.9.80:10086/stream}, dst=display}" 
    public partial class VedioRecord : Window
    {
        private const string serverip = "192.168.88.177";
        private const int port = 1233;
        static TcpClient client = null;
        static NetworkStream stream = null;
        Thread TcpRecvThread;
        Thread TcpConnectThread;
        System.Timers.Timer RecTimer;
        public const double TIMEOUT = 30000;

        // header  len    cam_id   zoom   focus   ir_cut  reset  status
        // 0x7d    0x07   0x01     0x00   0x00    0x00    0x00   0x02
        private Byte[] data = new Byte[] { 0x7d, 0x07, 0x01, 0x00, 0x00, 0x00, 0x00, 0x02};

        private bool FstFlag = false;
        private bool ConFlg = false;
        private bool ThreadStopFlg = false;
        private string filePath; 
        private string currentDirectory;
        public VedioRecord()
        {
            InitializeComponent();
            // 失能按钮
            btn_sys_ctl.Content = "关";
            btn_sys_ctl.IsChecked = false;

            Checker1.IsChecked = false;
            Checker1.IsEnabled = false;
            btn_focus_add.IsEnabled = false;
            btn_focus_dec.IsEnabled = false;
            btn_ir_cut.IsEnabled = false;
            btn_reset.IsEnabled = false;
            slider2.Value = 1;
            slider2.IsEnabled = false;
            slider2.AddHandler(MouseLeftButtonUpEvent, new MouseButtonEventHandler(slider2_MouseLeftButtonUp), true);

            RecTimer = new System.Timers.Timer(TIMEOUT);//实例化Timer类，设置间隔时间为30000毫秒 30s；
            RecTimer.Elapsed += new System.Timers.ElapsedEventHandler(Execute);//到达时间的时候执行事件；
            RecTimer.AutoReset = true;//设置是执行一次（false）还是一直执行(true)；
        }

        private void Connect()
        {
            this.Dispatcher.Invoke(() => { 
                FstFlag = false;
                btn_sys_ctl.Content = "关";
                btn_sys_ctl.IsEnabled = false;
                check_ip.IsEnabled = false;
                status_bar.Text = "正在连接，请稍后...";
                if ((bool)check_ip.IsChecked)
                {
                    System.Net.IPAddress[] addressList = Dns.GetHostByName(Dns.GetHostName()).AddressList;
                    status_bar.Text = "正在连接，本机ip：" + addressList[0].ToString();
                    if (addressList[0].ToString() != "192.168.88.88")
                    {
                        status_bar.Text = "ip配置错误，请将本机ip设置成 192.168.88.88";
                        btn_sys_ctl.Content = "关";
                        btn_sys_ctl.IsChecked = false;
                        check_ip.IsEnabled = true;
                        Thread.Sleep(20);
                        if (TcpConnectThread != null) TcpConnectThread.Abort();
                    }
                }
            });

            client = new TcpClient();
            var result = client.BeginConnect(serverip, port, null, null);

            result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(4));
            this.Dispatcher.Invoke(() => {
                if (!client.Connected)
                {
                    // connection failure
                    data[7] = 0x02;
                    btn_sys_ctl.Content = "关";
                    btn_sys_ctl.IsChecked = false;
                    check_ip.IsEnabled = true;
                    status_bar.Text = "连接失败，请检查网络设备速率是否为10M全双工 设备是否开启";
                }
                else
                {
                    // have connected
                    stream = client.GetStream();
                    
                    data[7] = 0x01;
                    status_bar.Text = "设备已连接";
                    
                    TcpRecvThread = new Thread(TcpReceive);
                    TcpRecvThread.IsBackground = true;
                    TcpRecvThread.Start();

                    if ((client != null) || (stream != null))
                    {
                        if (!FstFlag)
                        {
                            stream.Write(data, 0, data.Length);
                            stream.Flush();
                            FstFlag = true;
                        }
                    }
                    // 确保已发送连接信息
                    if (FstFlag)
                    {
                        VlcControl.SourceProvider.MediaPlayer.Play();
                        vlc_text.Visibility = Visibility.Collapsed;
                        status_bar.Text = "设备已连接";
                        ConFlg = true;
                        // 使能按钮
                        btn_sys_ctl.Content = "开";
                        btn_sys_ctl.IsChecked = true;
                        Checker1.IsEnabled = true;
                        btn_focus_add.IsEnabled = true;
                        btn_focus_dec.IsEnabled = true;
                        btn_ir_cut.IsEnabled = true;
                        btn_reset.IsEnabled = true;
                        slider2.IsEnabled = true;
                    }
                    else
                    {
                        btn_sys_ctl.Content = "关";
                        btn_sys_ctl.IsChecked = false;
                        status_bar.Text = "连接失败，请检查网络设备速率是否为10M全双工 设备是否开启";
                    }
                }
                // 连接操作之后 不管成功连接与否 均使能此二开关 以便再次操作
                check_ip.IsEnabled = true;
                btn_sys_ctl.IsEnabled = true;
            });
        }
        private void TcpSent()
        {
            if (((client != null) || (stream != null)) && client.Connected)
            {
                if (!FstFlag)
                {
                    stream.Write(data, 0, data.Length);
                    stream.Flush();
                }
                if (FstFlag && (data[7] == 0x03))
                {
                    stream = client.GetStream();
                    stream.Write(data, 0, data.Length);
                    stream.Flush();
                    data[7] = 0x01;
                }
                this.Dispatcher.Invoke(() =>
                {
                    //发送消息 不可操作
                    Checker1.IsEnabled = false;
                    btn_focus_add.IsEnabled = false;
                    btn_focus_dec.IsEnabled = false;
                    btn_ir_cut.IsEnabled = false;
                    btn_reset.IsEnabled = false;
                    slider2.IsEnabled = false;
                    // 每次发送消息时开启计时器
                    RecTimer.Interval = TIMEOUT;
                    if(data[7] != 0x02)
                    {
                        RecTimer.Start();
                    }
                });
            }
            else if (client != null)
            {
                if (!client.Connected)
                {
                    status_bar.Text = "设备未连接";
                    SysStop(1);
                }
            }
        }
        private void TcpReceive()
        {
            while (true)
            {
                Thread.Sleep(20);
                if ((client != null) || (stream != null))
                {
                    if (FstFlag)
                    {
                        Byte[] rec_data = new Byte[8];
                        stream.Read(rec_data, 0, rec_data.Length);
                        if ((rec_data[0] != 0x7d) && (rec_data[1] != 0x07))
                        {
                            //System.Windows.MessageBox.Show("传输错误，请重试");
                            data[7] = 0x02;
                        }
                        if (ThreadStopFlg)
                        {
                            this.Dispatcher.Invoke(() =>
                            {
                                // 断开连接
                                btn_sys_ctl.Content = "关";
                                btn_sys_ctl.IsChecked = false;
                                Checker1.IsEnabled = false;
                                btn_focus_add.IsEnabled = false;
                                btn_focus_dec.IsEnabled = false;
                                btn_ir_cut.IsEnabled = false;
                                btn_reset.IsEnabled = false;
                                slider2.Value = 1;
                                slider2.IsEnabled = false;
                            });
                        }
                        else
                        {
                            this.Dispatcher.Invoke(() =>
                            {
                                // 接收到返回消息后 可以随意操作
                                Checker1.IsEnabled = true;
                                btn_focus_add.IsEnabled = true;
                                btn_focus_dec.IsEnabled = true;
                                btn_ir_cut.IsEnabled = true;
                                btn_reset.IsEnabled = true;
                                slider2.IsEnabled = true;
                            });
                        }
                        // 获取状态位
                        data[7] = rec_data[7];
                        // reset位归零
                        data[6] = 0x00;
                        // ir_cut位归零
                        data[5] = 0x00;
                        // focus位归零
                        data[4] = 0x00;
                        // zoom位归零
                        data[3] = 0x00;
                        if (rec_data[7] == 0x03)
                        {
                            this.Dispatcher.Invoke(() => {
                                status_bar.Text = "已完成操作";
                            });
                        }
                        // 清空接收缓存
                        rec_data = new Byte[] { 0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00};
                        // 重置关闭连接定时器
                        RecTimer.Stop();
                    }
                }
                if (!client.Connected)
                {
                    status_bar.Text = "设备未连接";
                    FstFlag = false;
                    ConFlg = false;
                    data = new Byte[] { 0x7d, 0x07, 0x01, 0x00, 0x00, 0x00, 0x00, 0x02 };

                    if (TcpConnectThread != null)
                    {
                        TcpConnectThread.Abort();
                    }
                   
                    if (stream != null) stream.Close();
                    if (client != null) client.Close();
                    stream = null;
                    client = null;
                    this.Dispatcher.Invoke(() =>
                    {
                        //超时未操作 断开连接
                        btn_sys_ctl.Content = "关";
                        btn_sys_ctl.IsChecked = false;
                        Checker1.IsEnabled = false;
                        btn_focus_add.IsEnabled = false;
                        btn_focus_dec.IsEnabled = false;
                        btn_ir_cut.IsEnabled = false;
                        btn_reset.IsEnabled = false;
                        slider2.Value = 1;
                        slider2.IsEnabled = false;
                    });
                    // 退出tcp接收线程
                    if (TcpRecvThread != null)
                    {
                        TcpRecvThread.Abort();
                    }
                }
                // Console.WriteLine("tcp recv func running...\n");
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {  
            var currentAssembly = Assembly.GetEntryAssembly();
            currentDirectory = new FileInfo(currentAssembly.Location).DirectoryName;
            var libDirectory = new DirectoryInfo(System.IO.Path.Combine(currentDirectory, "libvlc", IntPtr.Size == 4 ? "win-x86" : "win-x64"));  
            
            this.VlcControl.SourceProvider.CreatePlayer(libDirectory); 
           // this.VlcControl.SourceProvider.MediaPlayer.LengthChanged += MediaPlayer_LengthChanged;
            this.VlcControl.SourceProvider.MediaPlayer.EncounteredError += MediaPlayer_EncounteredError;
            this.VlcControl.SourceProvider.MediaPlayer.SetVideoCallbacks(LockVideo, null, DisplayVideo, IntPtr.Zero);// //LockVideoCallback lockVideo, UnlockVideoCallback unlockVideo, DisplayVideoCallback display, IntPtr userData

        }
        private IntPtr LockVideo(IntPtr userdata, IntPtr planes)
        {
            Marshal.WriteIntPtr(planes, userdata);
            return userdata;
        }
        private void DisplayVideo(IntPtr userdata, IntPtr picture)
        {
            // Invalidates the bitmap
            this.Dispatcher.BeginInvoke((Action)(() =>
            {
                (this.VlcControl.SourceProvider.VideoSource as InteropBitmap)?.Invalidate();
                Console.WriteLine(GetCurrentTime());  
            })); 
        }

        private void MediaPlayer_EncounteredError(object sender, Vlc.DotNet.Core.VlcMediaPlayerEncounteredErrorEventArgs e)
        {
            Console.WriteLine(e.ToString());
        }

        private void sys_ctl(object sender, RoutedEventArgs e)
        {
            if((bool)btn_sys_ctl.IsChecked)
            {
                btn_sys_ctl.Content = "开";
                SysStart();
            }
            else
            {
                SysStop(0);
            }
        }
        

        public void Execute(object source, System.Timers.ElapsedEventArgs e)
        {
            SysStop(1);
            this.Dispatcher.Invoke(() =>
            {
                status_bar.Text = "设备掉线";
            });
        }

        public void SysStart()
        {
            ThreadStopFlg = false;
            /*uint ping_cnt_1 = 0;
            Ping ping_1 = new Ping();
            PingReply pingReply1 = ping_1.Send(serverip);
            if (pingReply1.Status == IPStatus.Success)
            {
                // Console.WriteLine("当前在线，已ping通！");
                status_bar.Text = "设备在线，正在连接，请稍后...";
            }
            else
            {
                btn_sys_ctl.IsChecked = false;
                SysStop(1);
                status_bar.Text = "设备不在线，请先打开设备";
                return;
                // Console.WriteLine("不在线，ping不通！");
            }*/
            string ed = "ts";
            string dest = Path.Combine(currentDirectory, $"record.{ed}");
            var options = new[]
            {
                    ":sout=#duplicate{dst=display,dst=std{access=file,mux="+ed+",dst=" +dest+"}}",
                    ":live-caching = 200",//本地缓存毫秒数
                    ":network-caching = 50",
                    //":sout=#file{dst=" + destination + "}",
                    //":sout=#duplicate{dst=display,dst=rtp{sdp=rtsp://:5544/cam}}", 想本地端口5544播放rtsp
                    ":sout-keep"// 持续开启串流输出 (默认关闭)

                   /* //":mmdevice-volume=0",
                    //":audiofile-channels=0",
                    ":live-caching = 300",//本地缓存毫秒数  display-audio :sout=#display
                    ":sout=#transcode{vcodec=h264,fps=25,venc=x264{preset=ultrafast,profile=baseline,tune=zerolatency},scale=1,acodec=mpga,ab=128,channels=2,samplerate=44100},",
                    ":duplicate{dst=display,dst=std{access=file,mux="+ed+",dst=" +dest+"}}",
                    ":sout=#duplicate{dst=display,dst=std{access=file,mux="+ed+",dst="+dest+"}}",
                    //":sout=#display",
                    ":sout-keep",
                    ":sout-all",
                    ":sout-audio",
                    ":sout-audio-sync",*/
            };
            this.VlcControl.SourceProvider.MediaPlayer.ResetMedia();
            //this.VlcControl.SourceProvider.MediaPlayer.SetMedia(new Uri("rtsp://192.168.88.141:8554/"), options);
            //this.VlcControl.SourceProvider.MediaPlayer.Play(new Uri("rtsp://192.168.88.141:8554/"));
            this.VlcControl.SourceProvider.MediaPlayer.SetMedia(new Uri("udp://@192.168.88.88:1234"), options);
            //this.VlcControl.SourceProvider.MediaPlayer.Play();

            TcpConnectThread = new Thread(Connect);
            TcpConnectThread.IsBackground = true;
            TcpConnectThread.Start();

            /*new Task(() =>
            {
                //这里开线程处理 实时ping设备
                uint cnt = 0;
                Ping ping_2 = new Ping();
                while (true)
                {
                    Thread.Sleep(2000);
                    PingReply pingReply = ping_2.Send(serverip);
                    if (pingReply.Status == IPStatus.Success)
                    {
                        // Console.WriteLine("当前在线，已ping通！");
                    }
                    else
                    {
                        cnt++;
                        // Console.WriteLine("不在线，ping不通！");
                    }
                    if (cnt == 3)
                    {
                        this.Dispatcher.Invoke(() =>
                        {
                            btn_sys_ctl.IsChecked = false;
                            SysStop(1);
                            status_bar.Text = "设备已下线";
                        });
                    }
                }
            }).Start();*/
            // Connect();
        }
        public void SysStop(uint val)
        {
            FstFlag = false;
            ConFlg = false;
            ThreadStopFlg = true;
            data = new Byte[] { 0x7d, 0x07, 0x01, 0x00, 0x00, 0x00, 0x00, 0x02 };
            if(val == 0)
            {
                TcpSent();
            }

            if (TcpConnectThread != null)
            {
                TcpConnectThread.Abort();
            }

            // 退出tcp接收线程
            //if (TcpRecvThread != null) TcpRecvThread.Join();
            if (TcpRecvThread != null)
            {
                TcpRecvThread.Abort();
            }

            if (stream != null)
            {
                stream.Close();
            }

            if (client != null)
            {
                client.Close();
            }

            stream = null;
            client = null;

            new Task(() =>
            {
                //这里要开线程处理，不然会阻塞播放 
                //Dispatcher.Invoke(() => { this.VlcControl.SourceProvider.MediaPlayer.Stop(); });
                this.VlcControl.SourceProvider.MediaPlayer.Stop();
                Dispatcher.Invoke(() => { vlc_text.Visibility = Visibility.Visible; });
            }).Start();

            this.Dispatcher.Invoke(() =>
            {
                status_bar.Text = "已断开连接";                
                btn_sys_ctl.Content = "关";
                btn_sys_ctl.IsChecked = false;
                Checker1.IsChecked = false;
                Checker1.IsEnabled = false;
                btn_focus_add.IsEnabled = false;
                btn_focus_dec.IsEnabled = false;
                btn_ir_cut.IsEnabled = false;
                btn_reset.IsEnabled = false;
                slider2.Value = 1;
                slider2.IsEnabled = false;
            });
        }

        private float lastPlayTime = 0;
        private float lastPlayTimeGlobal = 0;

        public float GetCurrentTime()
        {
            float currentTime = this.VlcControl.SourceProvider.MediaPlayer.Time;
            var tick = float.Parse(DateTime.Now.ToString("fff"));
            if (lastPlayTime == currentTime && lastPlayTime != 0)
            {
                currentTime += (tick - lastPlayTimeGlobal);
            }
            else
            {
                lastPlayTime = currentTime;
                lastPlayTimeGlobal = tick;
            }

            return currentTime * 0.001f;
        }

        private void focus_Add(object sender, RoutedEventArgs e)
        {
            int focus = 1;
            data[4] = (Byte)focus;
            if (client != null)
            {
                status_bar.Text = "正在执行一次微调+";
            }
            else
            {
                status_bar.Text = "设备未连接，请先关连接设备";
                return;
            }
            TcpSent();
        }

        private void focus_Dec(object sender, RoutedEventArgs e)
        {
            int focus = -1;
            data[4] = (Byte)focus;
            if (client != null)
            {
                status_bar.Text = "正在执行一次微调-";
            }
            else
            {
                status_bar.Text = "设备未连接，请先关连接设备";
                return;
            }
            TcpSent();
        }

        private void ir_cut(object sender, RoutedEventArgs e)
        {

            if ((bool)btn_ir_cut.IsChecked)
            {
                // 关操作
                btn_ir_cut.Content = "ir_cut关";
                data[5] = 0x02;

                if (client != null)
                {
                    status_bar.Text = "正在执行关闭ir_cut";
                }
                else
                {
                    btn_ir_cut.Content = "ir_cut开";
                    btn_ir_cut.IsChecked = false;
                    data[5] = 0x01;
                    status_bar.Text = "设备未连接，请先关连接设备";
                    return;
                }
            }
            else
            {
                // 开操作
                btn_ir_cut.Content = "ir_cut开";
                data[5] = 0x01;

                if (client != null)
                {
                    status_bar.Text = "正在执行开启ir_cut";
                }
                else
                {
                    btn_ir_cut.Content = "ir_cut关";
                    btn_ir_cut.IsChecked = true;
                    data[5] = 0x02;
                    status_bar.Text = "设备未连接，请先关连接设备";
                    return;
                }
            }


            TcpSent();
        }

        private void reset_cam(object sender, RoutedEventArgs e)
        {

            data[6] = 0x01;
            
            if (client != null)
            {
                slider2.Value = 5;
                status_bar.Text = "正在执行重置操作";
            }
            else
            {
                status_bar.Text = "设备未连接，请先关连接设备";
                return;
            }
            TcpSent();
        }

        private void Camera_Checked(object sender, RoutedEventArgs e)
        {
            if((bool)Checker1.IsChecked)
            {
                data[2] = 0x02;
                Checker1.Content = "相机2";
                if (client != null)
                {
                    status_bar.Text = "正在切换至相机2";
                }
                else
                {
                    status_bar.Text = "设备未连接，请先关连接设备";
                    return;
                }
            }
            else
            {
                data[2] = 0x01;
                Checker1.Content = "相机1";
                if (client != null)
                {
                    status_bar.Text = "正在切换至相机1";
                }
                else
                {
                    status_bar.Text = "设备未连接，请先关连接设备";
                    return;
                }
            }
            // 切换相机 zoom focus ir_cut reset 归零
            data[3] = 0;
            data[4] = 0;
            data[5] = 0;
            data[6] = 0;
            TcpSent();
        }


        private void slider2_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            data[3] = (byte)slider2.Value;
            if (client != null)
            {
                slider2.IsEnabled = false;
                status_bar.Text = "正在变焦至level" + data[3];
            }
            else
            {
                status_bar.Text = "设备未连接，请先关连接设备";
                return;
            }
            TcpSent();
        }

        private void window_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)

            {

                this.DragMove();

            }
        }
    }
}
