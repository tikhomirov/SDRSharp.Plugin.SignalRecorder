using SDRSharp.Radio;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace SDRSharp.Plugin.SignalRecorder
{
    public class SignalRecorderProcessor : IIQProcessor, INotifyPropertyChanged
    {
        public SignalRecorderProcessor()
        {
            // initial values
            SelectedFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            RecordingTime = 10;
            SampleCount = 0;
            _line = new StringBuilder();
        }

        private int _recordingTime;
        private int _timePerFile;
        private int _samplesToBeSaved;
        private int _samplesPerFile;
        private bool _recordingEnabled;
        private bool _recording;
        private string _selectedFolder;
        private StringBuilder _line;
        private FileStream _wavFileStream;
        private BinaryWriter _wavWriter;
        private long _wavDataSizePosition;
        private long _wavSampleCount;

        public double SampleRate { get; set; }

        public double SampleCount { get; set; }

        public int ThresholdDb { get; set; }

        public int RecordingTime
        {
            get => _recordingTime;
            set
            {
                _recordingTime = value;
                ResetSamplesToBeSaved();
            }
        }

        public int TimePerFile
        {
            get => _timePerFile;
            set
            {
                _timePerFile = value;
                ResetSamplesPerFile();
            }
        }


        // plugin enabled from the SDRSharp menu
        public bool Enabled { get; set; }

        public bool RecordingEnabled
        {
            get => _recordingEnabled;
            set
            {
                if (!value) 
                {
                    _recording = false;
                    // Close WAV file if it's open
                    if (WavOutputEnabled)
                    {
                        CloseWavFile();
                    }
                }
                // if enabled create a new file name
                else UpdateFileName();

                _recordingEnabled = value;
                RaisePropertyChanged(nameof(RecordingEnabled));
                RaisePropertyChanged(nameof(RecordingDisabled));
                RaisePropertyChanged(nameof(RecordingStatus));
            }
        }

        public bool RecordingDisabled { get => !RecordingEnabled; }

        public bool Recording
        {
            get => _recording;
            set
            {
                _recording = value;
                RaisePropertyChanged(nameof(RecordingStatus));
                RaisePropertyChanged(nameof(NotRecording));
            }
        }

        public bool NotRecording { get => !Recording; }

        public bool AutoRecord { get; set; }

        public bool ISaveEnabled { get; set; }

        public bool QSaveEnabled { get; set; }

        public bool ModSaveEnabled { get; set; }

        public bool ArgSaveEnabled { get; set; }

        public bool WavOutputEnabled { get; set; }

        public bool CsvOutputEnabled { get; set; } = true;

        public string SelectedFolder
        {
            get => _selectedFolder;
            set
            {
                _selectedFolder = value;
                RaisePropertyChanged(nameof(SelectedFolder));
            }
        }

        public string FileName { get; set; }

        public string RecordingStatus
        {
            get
            {
                if (RecordingEnabled)
                {
                    if (Recording) return "Recording";
                    else return "Waiting";
                }
                return "";
            }
        }

        #region Implementation of INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        private void RaisePropertyChanged([CallerMemberName] String propertyName = "")
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }


        #endregion

        public unsafe void Process(Complex* buffer, int length)
        {
            if (RecordingEnabled)
            {
                if (WavOutputEnabled)
                {
                    ProcessWavOutput(buffer, length);
                }
                else
                {
                    ProcessCsvOutput(buffer, length);
                }
            }
        }

        private unsafe void ProcessCsvOutput(Complex* buffer, int length)
        {
            const int BufferSize = 65536;  // 64 Kilobytes
            StreamWriter file = new StreamWriter(FileName, append: true, Encoding.UTF8, BufferSize);

            for (int i = 0; i < length; i++)
            {
                float modulus = buffer[i].Modulus();
                float db = 20 * (float)Math.Log10(modulus);

                if (db > ThresholdDb && RecordingEnabled && !Recording)
                {
                    Recording = true;

                    MakeFileHeader();
                    file.Write(_line);
                    _line.Clear();
                }

                if (Recording)
                {
                    _line.Append(SampleCount++ / SampleRate * 1000);
                    if (ISaveEnabled) _line.Append('\t').Append(buffer[i].Imag);
                    if (QSaveEnabled) _line.Append('\t').Append(buffer[i].Real);
                    if (ModSaveEnabled) _line.Append('\t').Append(modulus);
                    if (ArgSaveEnabled) _line.Append('\t').Append(buffer[i].Argument());
                    _line.Append('\n');
                    file.Write(_line);
                    _line.Clear();

                    // if neither full signal recording is selected
                    // nor a signal is detected, countdown the samples
                    if (!AutoRecord || db < ThresholdDb) _samplesToBeSaved--;
                    else ResetSamplesToBeSaved();

                    if (_samplesToBeSaved <= 0)
                    {
                        Recording = false;
                        RecordingEnabled = false;
                        SampleCount = 0;
                    }

                    if(_samplesPerFile > 0) _samplesPerFile--;
                    if(_samplesPerFile == 0 && Recording)
                    {
                        file.Close();
                        UpdateFileName();
                        ResetSamplesPerFile();
                        file = new StreamWriter(FileName, append: true, Encoding.UTF8, BufferSize);
                        MakeFileHeader();
                        file.Write(_line);
                        _line.Clear();
                    }
                }
            }

            file.Close();
        }

        private unsafe void ProcessWavOutput(Complex* buffer, int length)
        {
            for (int i = 0; i < length; i++)
            {
                float modulus = buffer[i].Modulus();
                float db = 20 * (float)Math.Log10(modulus);

                if (db > ThresholdDb && RecordingEnabled && !Recording)
                {
                    Recording = true;
                    StartWavFile();
                }

                if (Recording)
                {
                    // Write IQ data as stereo 32-bit float (I=left, Q=right)
                    _wavWriter.Write(buffer[i].Real);  // I component (left channel)
                    _wavWriter.Write(buffer[i].Imag);  // Q component (right channel)
                    _wavSampleCount++;
                    SampleCount++;

                    // if neither full signal recording is selected
                    // nor a signal is detected, countdown the samples
                    if (!AutoRecord || db < ThresholdDb) _samplesToBeSaved--;
                    else ResetSamplesToBeSaved();

                    if (_samplesToBeSaved <= 0)
                    {
                        Recording = false;
                        RecordingEnabled = false;
                        SampleCount = 0;
                        CloseWavFile();
                    }

                    if(_samplesPerFile > 0) _samplesPerFile--;
                    if(_samplesPerFile == 0 && Recording)
                    {
                        CloseWavFile();
                        UpdateFileName();
                        ResetSamplesPerFile();
                        StartWavFile();
                    }
                }
            }
        }

        private void ResetSamplesToBeSaved()
        {
            _samplesToBeSaved = (int)(SampleRate / 1000 * RecordingTime);
        }

        private void ResetSamplesPerFile()
        {
            _samplesPerFile = (int)(SampleRate / 1000 * TimePerFile);
        }

        private void UpdateFileName()
        {
            string extension = WavOutputEnabled ? ".wav" : ".csv";
            FileName = Path.Combine(SelectedFolder, "SigRec_" + DateTime.Now.ToString("yyyyMMddHHmmssff") + extension);
        }

        private void MakeFileHeader()
        {
            _line.Append("Sample time[ms]");
            if (ISaveEnabled) _line.Append('\t').Append("I");
            if (QSaveEnabled) _line.Append('\t').Append("Q");
            if (ModSaveEnabled) _line.Append('\t').Append("Modulus");
            if (ArgSaveEnabled) _line.Append('\t').Append("Argument");
            _line.Append('\n');
        }

        private void StartWavFile()
        {
            _wavFileStream = new FileStream(FileName, FileMode.Create, FileAccess.Write);
            _wavWriter = new BinaryWriter(_wavFileStream);
            _wavSampleCount = 0;

            // Write WAV header
            WriteWavHeader();
        }

        private void WriteWavHeader()
        {
            // RIFF header
            _wavWriter.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            _wavWriter.Write((uint)0); // File size placeholder (will be updated later)
            _wavWriter.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));

            // fmt chunk
            _wavWriter.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
            _wavWriter.Write((uint)16); // fmt chunk size
            _wavWriter.Write((ushort)3); // Audio format (3 = IEEE 754 float)
            _wavWriter.Write((ushort)2); // Number of channels (2 for stereo IQ)
            _wavWriter.Write((uint)SampleRate); // Sample rate
            _wavWriter.Write((uint)(SampleRate * 2 * 4)); // Byte rate (sample rate * channels * bytes per sample)
            _wavWriter.Write((ushort)(2 * 4)); // Block align (channels * bytes per sample)
            _wavWriter.Write((ushort)32); // Bits per sample (32-bit float)

            // data chunk
            _wavWriter.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            _wavDataSizePosition = _wavWriter.BaseStream.Position;
            _wavWriter.Write((uint)0); // Data size placeholder (will be updated later)
        }

        private void CloseWavFile()
        {
            if (_wavWriter != null)
            {
                // Update file size in RIFF header
                long currentPosition = _wavWriter.BaseStream.Position;
                _wavWriter.BaseStream.Seek(4, SeekOrigin.Begin);
                _wavWriter.Write((uint)(currentPosition - 8));

                // Update data size in data chunk
                _wavWriter.BaseStream.Seek(_wavDataSizePosition, SeekOrigin.Begin);
                _wavWriter.Write((uint)(_wavSampleCount * 2 * 4)); // samples * channels * bytes per sample

                _wavWriter.Close();
                _wavFileStream.Close();
                _wavWriter = null;
                _wavFileStream = null;
            }
        }

        public bool PlotValuesFromCsv()
        {
            // check if in the selected folder there is a valid file to be plotted
            var fileList = new DirectoryInfo(SelectedFolder).GetFiles("SigRec_*.csv");
            if (fileList.Any())
            {
                try
                {
                    var lastFile = fileList.Last().FullName;

                    new PlotForm(lastFile).ShowDialog();

                    return true;
                }
                catch
                {
                    return false;
                }
            }
            else return false;
        }
    }
}
