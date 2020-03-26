using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LibKidsNoteForEveryone.GoogleDrive
{
    public class MimeType
    {
        public static string get(string ext)
        {
            // https://github.com/google/google-drive-proxy/blob/master/DriveProxy/API/MimeType.cs#L145
            if (ext == "jpg")
            {
                return Constants.GOOGLE_DRIVE_MIMETYPE_JPG;
            }
            else if (ext == "png")
            {
                return Constants.GOOGLE_DRIVE_MIMETYPE_PNG;
            }
            else if (ext == "pdf")
            {
                return Constants.GOOGLE_DRIVE_MIMETYPE_PDF;
            }
            else if (ext == "doc")
            {
                return Constants.GOOGLE_DRIVE_MIMETYPE_MSWORD;
            }
            else if (ext == "docx")
            {
                return Constants.GOOGLE_DRIVE_MIMETYPE_MSWORD_DOCX;
            }
            else if (ext == "xls")
            {
                return Constants.GOOGLE_DRIVE_MIMETYPE_MSEXCEL;
            }
            else if (ext == "xlsx")
            {
                return Constants.GOOGLE_DRIVE_MIMETYPE_MSEXCEL_XLSX;
            }
            else if (ext == "ppt")
            {
                return Constants.GOOGLE_DRIVE_MIMETYPE_MSPPT;
            }
            else if (ext == "pptx")
            {
                return Constants.GOOGLE_DRIVE_MIMETYPE_MSPPT_PPTX;
            }
            else if (ext == "mp4")
            {
                return Constants.GOOGLE_DRIVE_MIMETYPE_MP4;
            }
            else if (ext == "mov")
            {
                return Constants.GOOGLE_DRIVE_MIMETYPE_MOV;
            }
            else
            {
                return Constants.GOOGLE_DRIVE_MIMETYPE_BIN;
            }
        }
    }
}
