using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WhisperLoopbackStream
{
    public class Transcript
    {
        public Transcript() {
            Text = "";  // FIXME
        }

        /// <summary>
        /// SegmentStart
        /// </summary>
        public TimeSpan SegmentStart { get; set; }

        /// <summary>
        /// SegmentEnd
        /// </summary>
        public TimeSpan SegmentEnd { get; set; }

        /// <summary>
        /// Text
        /// </summary>
        public String Text { get; set; }
    }

    public class TranscriptStorage
    {
        public ObservableCollection<Transcript> TranscriptList { get; set; } = new ObservableCollection<Transcript>();


    }
}
