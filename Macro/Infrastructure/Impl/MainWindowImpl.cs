﻿using Macro.Extensions;
using Macro.Infrastructure;
using Macro.Infrastructure.Manager;
using Macro.Infrastructure.Serialize;
using Macro.Models;
using Macro.View;
using MahApps.Metro.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using Utils;
using Utils.Extensions;
using Utils.Infrastructure;
using EventType = Macro.Models.EventType;
using InputManager = Macro.Infrastructure.Manager.InputManager;
using Message = Utils.Document.Message;
using Point = System.Windows.Point;
using Rect = Utils.Infrastructure.Rect;
using Version = Macro.Models.Version;

namespace Macro
{
    public partial class MainWindow : MetroWindow
    {
        private TaskQueue _taskQueue;
        private string _path;
        private int _index;
        private IEnumerable<KeyValuePair<string, Process>> _processes;
        private IConfig _config;
        private Bitmap _bitmap;
        private List<CaptureView> _captureViews;
        public MainWindow()
        {
            _index = 0;
            _taskQueue = new TaskQueue();
            _captureViews = new List<CaptureView>();

            InitializeComponent();
            Loaded += MainWindow_Loaded;
            }
        
        private void Init()
        {
            //window7 not support
            if (Environment.OSVersion.Version.Major >= 6 && Environment.OSVersion.Version.Minor > 1)
            {
                NativeHelper.SetProcessDpiAwareness(PROCESS_DPI_AWARENESS.PROCESS_PER_MONITOR_DPI_AWARE);
            }
            else
            {
                this.MessageShow("Error", DocumentHelper.Get(Message.FailedOSVersion));
            }
            foreach(var item in DisplayHelper.MonitorInfo())
            {
                _captureViews.Add(new CaptureView(item));
            }
            _config = ObjectExtensions.GetInstance<IConfig>();

            Refresh();
            ProcessManager.AddJob(OnProcessCallback);
            _taskQueue.Enqueue(SaveLoad, _path);
        }

        private void Refresh()
        {
            _path = _config.SavePath;
            if (string.IsNullOrEmpty(_path))
                _path = ConstHelper.DefaultSavePath;
            else
                _path += @"\";
            if (!Directory.Exists(_path))
                Directory.CreateDirectory(_path);
            _path = $"{_path}{ConstHelper.DefaultSaveFile}";

            _processes = Process.GetProcesses().Where(r=>r.MainWindowHandle != IntPtr.Zero).Select(r => new KeyValuePair<string, Process>(r.ProcessName, r));
            comboProcess.ItemsSource = _processes.OrderBy(r => r.Key);
            comboProcess.DisplayMemberPath = "Key";
            comboProcess.SelectedValuePath = "Value";

            var labels = ObjectExtensions.FindChildren<Label>(this);
            foreach (var label in labels)
            {
                BindingOperations.GetBindingExpressionBase(label, ContentProperty).UpdateTarget();
            }
            var buttons = ObjectExtensions.FindChildren<Button>(this);
            foreach (var button in buttons)
            {
                if (button.Equals(btnRefresh) || button.Equals(btnSetting))
                    continue;
                BindingOperations.GetBindingExpressionBase(button, ContentProperty).UpdateTarget();
            }
            BindingOperations.GetBindingExpressionBase(this, TitleProperty).UpdateTarget();
        }

        private bool TryModelValidate(EventTriggerModel model, out Message message)
        {
            message = Message.Success;
            model.KeyboardCmd = model.KeyboardCmd.Replace(" ", "");
            if (model.Image == null)
            {
                message = Message.FailedImageValidate;
                return false;
            }
            if (model.EventType == EventType.Mouse && model.MousePoint == null)
            {
                message = Message.FailedMouseCoordinatesValidate;
                return false;
            }

            if (string.IsNullOrEmpty(model.KeyboardCmd) && model.EventType == EventType.Keyboard)
            {
                message = Message.FailedKeyboardCommandValidate;
                return false;
            }
            if (string.IsNullOrEmpty(model.ProcessInfo.ProcessName))
            {
                message = Message.FailedProcessValidate;
                return false;
            }
            return true;
        }
        private void Capture()
        {
            Clear();
            WindowState = WindowState.Minimized;

            foreach(var item in _captureViews)
            {
                item.ShowActivate();
            }
        }
        private void Clear()
        {
            btnDelete.Visibility = Visibility.Collapsed;
            _bitmap = null;
            captureImage.Background = System.Windows.Media.Brushes.White;
            configView.Clear();
        }
        private Task Delete(object m)
        {
            if (File.Exists(_path))
            {
                File.Delete(_path);
                using (var fs = new FileStream(_path, FileMode.CreateNew))
                {
                    foreach (var data in (configView.DataContext as Models.ViewModel.ConfigEventViewModel).TriggerSaves)
                    {
                        var bytes = ObjectSerializer.SerializeObject(data);
                        fs.Write(bytes, 0, bytes.Count());
                    }
                    fs.Close();
                }
            }
            return Task.CompletedTask;
        }
        private Task Save(object m)
        {
            var model = m as EventTriggerModel;
            model.Index = _index++;
            using (var fs = new FileStream(_path, FileMode.Append))
            {
                var bytes = ObjectSerializer.SerializeObject(model);
                fs.Write(bytes, 0, bytes.Count());
                fs.Close();
            }
            return Task.CompletedTask;
        }
        private Task SaveLoad(object state)
        {
            var task = new TaskCompletionSource<Task>();
            Dispatcher.Invoke(() => 
            {
                try
                {
                    var models = ObjectSerializer.DeserializeObject<EventTriggerModel>(File.ReadAllBytes(_path));
                    foreach (var model in models)
                    {
                        _index = _index < model.Index ? model.Index : _index;
                        configView.InsertModel(model);
                    }
                    task.SetResult(Task.CompletedTask);
                }
                catch (Exception ex)
                {
                    File.Delete(_path);
                    LogHelper.Warning(ex.Message);
                    Task.FromException(new FileLoadException(DocumentHelper.Get(Message.FailedLoadSaveFile)));
                }
            }, DispatcherPriority.Send);
            return task.Task;
        }
        private void VersionCheck()
        {
            var request = (HttpWebRequest)WebRequest.Create(ConstHelper.VersionUrl);
            Version version = null;
            using (var response = request.GetResponse())
            {
                using (var stream = new StreamReader(response.GetResponseStream()))
                {
                    version = JsonHelper.DeserializeObject<Version>(stream.ReadToEnd());
                }
            }
            if(version != null)
            {
                if(version.CompareTo(Version.CurrentVersion) > 0)
                {
                    if(this.MessageShow("Infomation", DocumentHelper.Get(Message.NewVersion), MahApps.Metro.Controls.Dialogs.MessageDialogStyle.AffirmativeAndNegative) == MahApps.Metro.Controls.Dialogs.MessageDialogResult.Affirmative)
                    {
                        Process.Start(ConstHelper.ReleaseUrl);
                    }
                }
            }
        }
        private Task OnProcessCallback()
        {
            var task = new TaskCompletionSource<Task>();
            List<EventTriggerModel> saves = null;
            Dispatcher.Invoke(() => 
            {
                saves = configView.TriggerSaves;
            });
            if(saves != null)
            {
                foreach (var save in saves)
                {
                    var processes = _processes.Where(r => r.Key.Equals(save.ProcessInfo.ProcessName)).ToList();
                    foreach (var process in processes)
                    {
                        if (DisplayHelper.ProcessCapture(process.Value, out Bitmap bmp))
                        {
                            var factor = NativeHelper.GetSystemDpi();
                            var sourceBmp = bmp.Resize((int)Math.Truncate(bmp.Width * (factor.X / ConstHelper.DefaultDPI)), (int)Math.Truncate(bmp.Height * (factor.Y / ConstHelper.DefaultDPI)));
                            
                            var souceFactorX = factor.X / (save.MonitorInfo.Dpi.X * 1.0F);
                            var souceFactorY = factor.Y / (save.MonitorInfo.Dpi.Y * 1.0F);

                            var targetBmp = save.Image.Resize((int)Math.Truncate(save.Image.Width * souceFactorX), (int)Math.Truncate(save.Image.Height * souceFactorY));

                            Dispatcher.Invoke(() =>
                            {
                                captureImage.Background = new ImageBrush(bmp.ToBitmapSource());
                            });
                            var similarity = OpenCVHelper.Search(sourceBmp, targetBmp, out Point location);
                            LogHelper.Debug($"Similarity : {similarity} % max Loc : X : {location.X} Y: {location.Y}");

                            if (similarity >= _config.Similarity)
                            {
                                if (save.EventType == EventType.Mouse)
                                {
                                    LogHelper.Debug($"[index : {save.Index}] save Mouse X : {save.MousePoint.Value.X} save Mouse Y : {save.MousePoint.Value.Y}");

                                    var currentPosition = new Rect();
                                    NativeHelper.GetWindowRect(process.Value.MainWindowHandle, ref currentPosition);

                                    var targetFactorX = 1.0F;
                                    var targetFactorY = 1.0F;

                                    foreach (var monitor in DisplayHelper.MonitorInfo())
                                    {
                                        if (monitor.Rect.IsContain(currentPosition))
                                        {
                                            targetFactorX = monitor.Dpi.X / (save.MonitorInfo.Dpi.X * targetFactorX);
                                            targetFactorY = monitor.Dpi.Y / (save.MonitorInfo.Dpi.Y * targetFactorY);
                                            break;
                                        }
                                    }

                                    var mousePosition = new Point()
                                    {
                                        X = Math.Abs(save.ProcessInfo.Position.Left + save.MousePoint.Value.X * -1) * targetFactorX,
                                        Y = Math.Abs(save.ProcessInfo.Position.Top + save.MousePoint.Value.Y * -1) * targetFactorY
                                    };

                                    NativeHelper.PostMessage(process.Value.MainWindowHandle, WindowMessage.LButtonDown, 1, mousePosition.ToLParam());
                                    Task.Delay(200);
                                    NativeHelper.PostMessage(process.Value.MainWindowHandle, WindowMessage.LButtonUp, 0, mousePosition.ToLParam());
                                }
                                else if (save.EventType == EventType.Keyboard)
                                {
                                    var hWndActive = NativeHelper.GetForegroundWindow();
                                    Task.Delay(200);
                                    NativeHelper.SetForegroundWindow(process.Value.MainWindowHandle);
                                    var commands = save.KeyboardCmd.Split('+');
                                    var modifiedKey = commands.Where(r =>
                                    {
                                        var keyCode = (KeyCode)Enum.Parse(typeof(KeyCode), $"{r}", true);
                                        return keyCode.IsExtendedKey();
                                    }).Select(r =>
                                    {
                                        var keyCode = (KeyCode)Enum.Parse(typeof(KeyCode), $"{r}", true);
                                        return keyCode;
                                    });

                                    var keys = commands.Where(r =>
                                    {
                                        var keyCode = (KeyCode)Enum.Parse(typeof(KeyCode), r, true);
                                        return !keyCode.IsExtendedKey();
                                    }).Select(r =>
                                    {
                                        var keyCode = (KeyCode)Enum.Parse(typeof(KeyCode), r, true);
                                        return keyCode;
                                    });
                                    ObjectExtensions.GetInstance<InputManager>().Keyboard.ModifiedKeyStroke(modifiedKey, keys);
                                    NativeHelper.SetForegroundWindow(hWndActive);
                                }
                            }
                        }
                    }
                    Task.Delay(_config.ProcessDelay);
                }
                task.SetResult(Task.CompletedTask);
            }
            else
            {
                task.TrySetCanceled();
            }
            return task.Task;
        }
    }
}
