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

        // header  len    cam_id   zoom   focus   ir_cut  reset  status
        // 0x7d    0x07   0x01     0x00   0x00    0x00    0x00   0x02
        private Byte[] data = new Byte[] { 0x7d, 0x07, 0x01, 0x00, 0x00, 0x00, 0x00, 0x02};

        private bool FstFlag = false;
        private bool ConFlg = false;
        private string filePath; 
        private string currentDirectory;
        public VedioRecord()
        {
            InitializeComponent();
            // 失能按钮
            btnStop.IsEnabled = false;
            btnStop.IsEnabled = false;
            Checker1.IsEnabled = false;
            btn_focus_add.IsEnabled = false;
            btn_focus_dec.IsEnabled = false;
            btn_ir_cut.IsEnabled = false;
            btn_reset.IsEnabled = false;
        }

        /*        private void Connect()
                {
                    FstFlag = false;
                    this.Dispatcher.Invoke(() => {
                        btnOpenRTSP.IsEnabled = false;
                    });
                    try
                    {
                        client = new TcpClient(serverip, port);
                        stream = client.GetStream();
                        // if connected set status as ready
                        data[7] = 0x01;
                        //通过下面方法才能访问主线程的控件
                        this.Dispatcher.Invoke(() => {
                            status_bar.Text = "设备已连接";
                            btnOpenRTSP.IsEnabled = false;
                            btnStop.IsEnabled = true;
                        });
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                        data[7] = 0x02;
                        //通过下面方法才能访问主线程的控件
                        this.Dispatcher.Invoke(() => {
                            btnOpenRTSP.IsEnabled = true;
                            btnStop.IsEnabled = false;
                            status_bar.Text = "连接失败，请检查网络配置";
                        });
                    }
                    Thread.Sleep(20);

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
                    if(FstFlag)
                    {
                        this.Dispatcher.Invoke(() => {
                            status_bar.Text = "设备已连接";
                            // 失能按钮
                            btnOpenRTSP.IsEnabled = false;
                            // 使能按钮
                            btnStop.IsEnabled = true;
                            Checker1.IsEnabled = true;
                            btn_focus_add.IsEnabled = true;
                            btn_focus_dec.IsEnabled = true;
                            btn_ir_cut.IsEnabled = true;
                            btn_reset.IsEnabled = true;
                        });
                    }
                    else
                    {
                        this.Dispatcher.Invoke(() => {
                            btnOpenRTSP.IsEnabled = true;
                            btnStop.IsEnabled = false;
                            status_bar.Text = "连接失败，请检查网络配置";
                        });
                    }

                }*/

        private void Connect()
        {
            this.Dispatcher.Invoke(() => {
                FstFlag = false;
                btnOpenRTSP.IsEnabled = false;
            });


                client = new TcpClient();
                var result = client.BeginConnect(serverip, port, null, null);

                result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(4));
            this.Dispatcher.Invoke(() => {
                if (!client.Connected)
                {
                    // connection failure
                    data[7] = 0x02;
                    btnOpenRTSP.IsEnabled = true;
                    btnStop.IsEnabled = false;
                    status_bar.Text = "连接失败，请检查网络配置";
                }
                else
                {
                    // have connected
                    stream = client.GetStream();
                    
                    data[7] = 0x01;
                    status_bar.Text = "设备已连接";
                    btnOpenRTSP.IsEnabled = false;
                    btnStop.IsEnabled = true;

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
                        status_bar.Text = "设备已连接";
                        ConFlg = true;
                        // 失能按钮
                        btnOpenRTSP.IsEnabled = false;
                        // 使能按钮
                        btnStop.IsEnabled = true;
                        Checker1.IsEnabled = true;
                        btn_focus_add.IsEnabled = true;
                        btn_focus_dec.IsEnabled = true;
                        btn_ir_cut.IsEnabled = true;
                        btn_reset.IsEnabled = true;
                        level_1.IsEnabled = true;
                        level_2.IsEnabled = true;
                        level_3.IsEnabled = true;
                        level_4.IsEnabled = true;
                        level_5.IsEnabled = true;
                        level_6.IsEnabled = true;
                        level_7.IsEnabled = true;
                        level_8.IsEnabled = true;
                    }
                    else
                    {
                        btnOpenRTSP.IsEnabled = true;
                        btnStop.IsEnabled = false;
                        status_bar.Text = "连接失败，请检查网络配置";
                    }
                }
            });
        }
        private void TcpSent()
        {
            if ((client != null) || (stream != null))
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
                // System.Windows.MessageBox.Show("接收成功");
            }
        }
        private void TcpReceive()
        {
            while (true)
            {
                //Thread.Sleep(20);
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
                        Console.WriteLine(rec_data[7]);
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

        private void openrtsp_Click(object sender, RoutedEventArgs e)
        {
            status_bar.Text = "正在连接，请稍后...";
            string ed = "ts";
            string dest = Path.Combine(currentDirectory, $"record.{ed}");
            var options = new[]
            {
                    ":sout=#duplicate{dst=display,dst=std{access=file,mux="+ed+",dst=" +dest+"}}",
                    //":live-caching = 200",//本地缓存毫秒数
                    ":network-caching = 100",
                    //":sout=#file{dst=" + destination + "}",
                    //":sout=#duplicate{dst=display,dst=rtp{sdp=rtsp://:5544/cam}}", 想本地端口5544播放rtsp
                    ":sout-keep"// 持续开启串流输出 (默认关闭)

                   /* //":mmdevice-volume=0",
                    //":audiofile-channels=0",
                    :live-caching = 300",//本地缓存毫秒数  display-audio :sout=#display
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
            // Connect();
        }

        private void stop_Click(object sender, RoutedEventArgs e)
        {
            FstFlag = false;
            ConFlg = false;
            data = new Byte[] { 0x7d, 0x07, 0x01, 0x00, 0x00, 0x00, 0x00, 0x02 };
            TcpSent();

            TcpConnectThread.Abort();

            // 退出tcp接收线程
            TcpRecvThread.Abort();
            if (stream != null) stream.Close();
            if (client != null) client.Close();
            stream = null;
            client = null;
            status_bar.Text = "已断开连接";

            // combobox1.SelectedValue = 1;

            new Task(() =>
            {
                //这里要开线程处理，不然会阻塞播放 
                //Dispatcher.Invoke(() => { this.VlcControl.SourceProvider.MediaPlayer.Stop(); });
                this.VlcControl.SourceProvider.MediaPlayer.Stop(); 
            }).Start();
            btnOpenRTSP.IsEnabled = true;
            btnStop.IsEnabled = false;
            Checker1.IsEnabled = false;
            btn_focus_add.IsEnabled = false;
            btn_focus_dec.IsEnabled = false;
            btn_ir_cut.IsEnabled = false;
            btn_reset.IsEnabled = false;
            level_1.IsEnabled = false;
            level_2.IsEnabled = false;
            level_3.IsEnabled = false;
            level_4.IsEnabled = false;
            level_5.IsEnabled = false;
            level_6.IsEnabled = false;
            level_7.IsEnabled = false;
            level_8.IsEnabled = false;

            level_1.IsChecked = false;
            level_2.IsChecked = false;
            level_3.IsChecked = false;
            level_4.IsChecked = false;
            level_5.IsChecked = false;
            level_6.IsChecked = false;
            level_7.IsChecked = false;
            level_8.IsChecked = false;
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
                status_bar.Text = "设备未连接，请先点击开启连接设备";
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
                status_bar.Text = "设备未连接，请先点击开启连接设备";
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
                    status_bar.Text = "设备未连接，请先点击开启连接设备";
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
                    status_bar.Text = "设备未连接，请先点击开启连接设备";
                    return;
                }
            }


            TcpSent();
        }

        private void reset_cam(object sender, RoutedEventArgs e)
        {

            data[6] = 0x01;
            level_1.IsChecked = false;
            level_2.IsChecked = false;
            level_3.IsChecked = false;
            level_4.IsChecked = false;
            level_5.IsChecked = false;
            level_6.IsChecked = false;
            level_7.IsChecked = false;
            level_8.IsChecked = false;

            if (client != null)
            {
                status_bar.Text = "正在执行重置操作";
            }
            else
            {
                status_bar.Text = "设备未连接，请先点击开启连接设备";
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
                    status_bar.Text = "设备未连接，请先点击开启连接设备";
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
                    status_bar.Text = "设备未连接，请先点击开启连接设备";
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

/*        private void combobox1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string combox_text = Convert.ToString(combobox1.SelectedItem).Replace("System.Windows.Controls.ComboBoxItem: ", "");
            if(combox_text == "连接后请选择相机")
            {
                data[2] = 0x00;
                if (client != null)
                {
                    status_bar.Text = "请选择相机编号";
                }
                else
                {
                    status_bar.Text = "设备未连接，请先点击开启连接设备";
                    return;
                }
            }
            else if (combox_text == "相机1")
            {
                data[2] = 0x01;
                if (client != null)
                {
                    status_bar.Text = "选择相机1";
                }
                else
                {
                    status_bar.Text = "设备未连接，请先点击开启连接设备";
                    return;
                }
            }
            else if (combox_text == "相机2")
            {
                data[2] = 0x02;
                if (client != null)
                {
                    status_bar.Text = "选择相机2";
                }
                else
                {
                    status_bar.Text = "设备未连接，请先点击开启连接设备";
                    return;
                }
            }
            else
            {
                data[2] = 0x00;
            }
            TcpSent();
        }*/

        private void level_n_Checked(object sender, RoutedEventArgs e)
        {
            if(level_1.IsChecked == true)
            {
                data[3] = 0x01;
                if (client != null)
                {
                    status_bar.Text = "正在变焦至leve 1";
                }
                else
                {
                    status_bar.Text = "设备未连接，请先点击开启连接设备";
                    return;
                }
            }

            if (level_2.IsChecked == true)
            {
                data[3] = 0x02;
                if (client != null)
                {
                    status_bar.Text = "正在变焦至leve 2";
                }
                else
                {
                    status_bar.Text = "设备未连接，请先点击开启连接设备";
                    return;
                }
            }

            if (level_3.IsChecked == true)
            {
                data[3] = 0x03;
                if (client != null)
                {
                    status_bar.Text = "正在变焦至leve 3";
                }
                else
                {
                    status_bar.Text = "设备未连接，请先点击开启连接设备";
                    return;
                }
            }

            if (level_4.IsChecked == true)
            {
                data[3] = 0x04;
                if (client != null)
                {
                    status_bar.Text = "正在变焦至leve 4";
                }
                else
                {
                    status_bar.Text = "设备未连接，请先点击开启连接设备";
                    return;
                }
            }

            if (level_5.IsChecked == true)
            {
                data[3] = 0x05;
                if (client != null)
                {
                    status_bar.Text = "正在变焦至leve 5";
                }
                else
                {
                    status_bar.Text = "设备未连接，请先点击开启连接设备";
                    return;
                }
            }

            if (level_6.IsChecked == true)
            {
                data[3] = 0x06;
                if (client != null)
                {
                    status_bar.Text = "正在变焦至leve 6";
                }
                else
                {
                    status_bar.Text = "设备未连接，请先点击开启连接设备";
                    return;
                }
            }

            if (level_7.IsChecked == true)
            {
                data[3] = 0x07;
                if (client != null)
                {
                    status_bar.Text = "正在变焦至leve 7";
                }
                else
                {
                    status_bar.Text = "设备未连接，请先点击开启连接设备";
                    return;
                }
            }

            if (level_8.IsChecked == true)
            {
                data[3] = 0x08;
                if (client != null)
                {
                    status_bar.Text = "正在变焦至leve 8";
                }
                else
                {
                    status_bar.Text = "设备未连接，请先点击开启连接设备";
                    return;
                }
            }
            TcpSent();
        }

        
    }
}
