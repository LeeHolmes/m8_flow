using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Speech.Synthesis;
using System.Collections.Generic;
using System.Drawing.Text;
using Microsoft.Win32;

namespace m8_flow
{
    class m8_flow
    {
        [DllImport("user32.dll")]
        static extern bool SetProcessDPIAware();

        static void Main(string[] args)
        {
            SetProcessDPIAware();

            // Connect to the m8c window
            Process captureWindow = null;
            try
            {
                captureWindow = Process.GetProcessesByName("m8c")[0];
            }
            catch
            {
                captureWindow = Process.Start("m8c.exe");
            }

            // Detect DPI settings
            Graphics systemContext = Graphics.FromHwnd(captureWindow.MainWindowHandle);

            // Prepare the speech synthesizer
            SpeechSynthesizer synthesizer = new SpeechSynthesizer();
            synthesizer.SelectVoice("Microsoft Zira Desktop");
            synthesizer.SetOutputToDefaultAudioDevice();
            synthesizer.Volume /= 4;

            // Create a reference bitmap of the m8 font so that we can detect letters while screen
            // scraping from the m8c window.
            PrivateFontCollection fontCollection = new PrivateFontCollection();
            fontCollection.AddFontFile("m8stealth57.ttf");
            Font f = new Font(fontCollection.Families[0], (12f / (systemContext.DpiY / 96f)));
            Bitmap m8Font = new Bitmap(255 * 12, 14);
            Graphics g = Graphics.FromImage(m8Font);

            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.None;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.FillRectangle(Brushes.Black, new Rectangle(0, 0, m8Font.Width, m8Font.Height));

            for(int character = 0; character < 255; character++)
            {
                g.DrawString(String.Format("{0}", (char)character), f, Brushes.White, character * 10, 0);
            }
            g.Flush();

            int[,,] fontPixelData = new int[255,14,10];
            for (int currentChar = 0; currentChar < 255; currentChar++)
            {
                for (int y = 0; y < 14; y++)
                {
                    for (int x = 0; x < 10; x++)
                    {
                        Color m8Pixel = m8Font.GetPixel((currentChar * 10) + x + 3, y);
                        int fontPixelValue = m8Pixel.R + m8Pixel.G + m8Pixel.B;
                        if (fontPixelValue > 0) { fontPixelValue = 1; }

                        fontPixelData[currentChar, y, x] = fontPixelValue;
                    }
                }
            }

            int characters = 0;
            int tests = 0;

            Console.Clear();

            // Prepare the screen buffer to hold the text version of the m8c window
            string currentPage = "";
            char[][] screenBuffer = new char[24][];
            for(int row = 0; row < 24; row++)
            {
                screenBuffer[row] = new String(' ', 40).ToCharArray();
            }

            // Information about the current selection
            StringBuilder selectionContents = new StringBuilder();
            String lastSelectionContents = "";
            String lastScreenContents = "";
            int lastSelectionX = 0, lastSelectionY = 0;
            bool selectionChanged = false;

            Thread.Sleep(2000);
            while (true)
            {
                // Take a capture of the m8c window
                Bitmap toBeRecognized = null;
                try
                {
                    toBeRecognized = WindowCapture.CaptureWindow(captureWindow);
                }
                catch
                {
                    Thread.Sleep(500);
                    continue;
                }

                // If it's been DPI scaled on, apply the Windows compatibility setting to disable DPI scaling for m8c.
                if(toBeRecognized.Width > 640)
                {
                    Console.WriteLine("High DPI monitor detected, applying DPI fix to M8C. Please launch m8_flow again.");

                    string processPath = captureWindow.MainModule.FileName;
                    RegistryKey appCompatKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers", true);
                    appCompatKey.SetValue(processPath, "~ HIGHDPIAWARE");

                    captureWindow.Kill();
                    return;
                }

                selectionChanged = false;

                // Lock the data of the captured bitmap so we can access the memory directly.
                Rectangle rect = new Rectangle(0, 0, toBeRecognized.Width, toBeRecognized.Height);
                System.Drawing.Imaging.BitmapData bmpData =
                           toBeRecognized.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadWrite,
                           toBeRecognized.PixelFormat);

                // Get the address of the first line.
                IntPtr ptr = bmpData.Scan0;

                // Declare an array to hold the bytes of the bitmap.
                int bytes = Math.Abs(bmpData.Stride) * toBeRecognized.Height;
                byte[] toBeRecognizedData = new byte[bytes];

                // Copy the RGB values into the array.
                Marshal.Copy(ptr, toBeRecognizedData, 0, bytes);

                int selectionX = int.MinValue;
                int selectionY = int.MinValue;
                int bestSelectionLength = int.MinValue;

                // Find the selection
                for (int imageY = 66; imageY < toBeRecognized.Height - 19; imageY += 20)
                {
                    for (int imageX = 16; imageX < toBeRecognized.Width - 15; imageX += 16)
                    {
                        int selectionOffset, selectionBlueTop, selectionBlueBottom, selectionBlue;

                        // Detect a selection, start top and bottom. See if we have a run of blue pixels
                        // that start just before the current row (3 pixels below) with a corresponding blue pixel
                        // 3 pixels past the current row.
                        int columnTest = 0;
                        do
                        {
                            selectionOffset = bmpData.Stride * (imageY - 3) + (4 * (imageX + columnTest));
                            selectionBlueTop = toBeRecognizedData[selectionOffset];

                            selectionOffset = bmpData.Stride * (imageY + 14 + 3) + (4 * (imageX + columnTest));
                            selectionBlueBottom = toBeRecognizedData[selectionOffset];

                            selectionBlue = selectionBlueTop & selectionBlueBottom;
                            columnTest++;
                        } while (selectionBlue > 240);

                        // If the horizontal run was 10 more more pixels, we likely have an actual selection.
                        if (columnTest > 9)
                        {
                            // If this is the longest one so far, remember this area as the "best selection"
                            if (columnTest >= bestSelectionLength)
                            {
                                bestSelectionLength = columnTest;
                                selectionX = imageX;
                                selectionY = imageY;
                                if ((selectionX != lastSelectionX) || (selectionY != lastSelectionY))
                                {
                                    lastSelectionX = selectionX;
                                    lastSelectionY = selectionY;
                                    selectionChanged = true;
                                }
                            }
                        }
                    }
                }

                // If we can't find any selection, the window was likely being obscured - just loop until we can
                // see it again.
                if(selectionX < 0 || selectionY < 0)
                {
                    continue;
                }

                // Scan through the rows and columns of the captured image to extract the characters.
                for (int imageY = 66; imageY < toBeRecognized.Height - 19; imageY += 20)
                {
                    for (int imageX = 16; imageX < toBeRecognized.Width - 15; imageX += 16)
                    {
                        char bestCharacter = ' ';

                        // Go through the ASCII range to see which character it is.
                        for (int currentChar = 0; currentChar < 255; currentChar++)
                        {
                            characters++;
                            int pixelMismatches = 0;
                            int avgRed = 0, avgGreen = 0, avgBlue = 0;
                            int pixelsProcessed = 0;

                            // Test the pixels of the characters at even offset
                            // Doing these with offsets reduces the number of comparisons we have to make
                            // on average, as we don't spend as much time looking down runs of duplicated pixels
                            TestCharacterPixels(0, fontPixelData,
                                ref tests, bmpData, toBeRecognizedData, selectionX, selectionY,
                                bestSelectionLength, imageY, imageX, currentChar,
                                ref pixelMismatches, ref avgRed, ref avgGreen, ref avgBlue, ref pixelsProcessed);

                            if (pixelMismatches > 1) { continue; }

                            // Test the pixels of the characters at odd offset
                            // Doing these with offsets reduces the number of comparisons we have to make
                            // on average, as we don't spend as much time looking down runs of duplicated pixels
                            TestCharacterPixels(1, fontPixelData,
                                ref tests, bmpData, toBeRecognizedData, selectionX, selectionY,
                                bestSelectionLength, imageY, imageX, currentChar,
                                ref pixelMismatches, ref avgRed, ref avgGreen, ref avgBlue, ref pixelsProcessed);

                            if (pixelMismatches > 1) { continue; }

                            // Calcualate the average R,G,B of the character
                            avgRed /= pixelsProcessed;
                            avgGreen /= pixelsProcessed;
                            avgBlue /= pixelsProcessed;

                            // If we found a character with zero pixel mismatches, then remember what character it was.
                            bestCharacter = (char)currentChar;

                            // If the cell was completely black and we processed all the pixels, then it's a space.
                            if ((avgRed + avgGreen + avgBlue == 0) && (pixelsProcessed == (10 * 14)))
                            {
                                bestCharacter = ' ';
                            }

                            break;
                        }

                        if(bestCharacter == '\0')
                        {
                            bestCharacter = ' ';
                        }
                        screenBuffer[imageY / 20][imageX / 16] = bestCharacter;

                        // If this was a selection, store it
                        if (
                            (imageY >= selectionY) &&
                            (imageY < (selectionY + 14)) &&
                            (imageX >= selectionX) &&
                            (imageX < (selectionX + bestSelectionLength)))
                        {
                            selectionContents.Append(bestCharacter);
                        }
                    }
                }

                // Create a simple text representation of the characters in the screen
                StringBuilder screenContents = new StringBuilder();
                for(int row = 0; row < 24; row++)
                {
                    screenContents.AppendLine(new string(screenBuffer[row]));
                }

                // Determine whether the selection has changed, so we can decide what to re-announce
                String selectionContentsString = selectionContents.ToString();
                if(selectionContentsString != lastSelectionContents)
                {
                    selectionChanged = true;
                }
                lastSelectionContents = selectionContentsString;
                screenContents.AppendFormat("Current Selection: {0}", selectionContentsString);
          
                // Determine whether the screen has changed, so we can decide what to re-announce.
                String screenContentsString = screenContents.ToString();
                if ((screenContentsString != lastScreenContents) || selectionChanged)
                {
                    Console.Clear();
                    Console.WriteLine(screenContentsString);
                    lastScreenContents = screenContentsString;

                    try
                    {
                        ProcessScreen(synthesizer, screenBuffer, selectionContents.ToString(), selectionY / 20,
                            selectionX / 16, ref currentPage, selectionChanged);
                    }
                    catch { continue;  }
                }
                screenContents.Clear();
                selectionContents.Clear();
            }
        }

        // Processes the text version of the screen to announce the relevant details
        private static void ProcessScreen(SpeechSynthesizer synthesizer, char[][] screenBuffer, string selectionContents,
            int selectionY, int selectionX, ref string currentPage, bool selectionChanged)
        {
            // If there was nothing selected, use the wording of "No Value"
            if(String.IsNullOrEmpty(selectionContents.Trim()))
            {
                selectionContents = "No value";
            }

            // Extract the title of the page. If neither the page changed nor the selection changed, no need to announce
            // anything
            string pageTitle = new String(screenBuffer[3], 1, 10);
            if((pageTitle == currentPage) && (! selectionChanged))
            {
                return;
            }

            // If we do need to speak something, cancel any speaking that's currently happening
            synthesizer.SpeakAsyncCancelAll();

            // Check if the page has changed. If so, announce it.
            if (pageTitle != currentPage)
            {
                currentPage = pageTitle;

                // Expand some abbreviations
                if (pageTitle.StartsWith("INST"))
                {
                    pageTitle = pageTitle.Replace("INST.", "Instrument");

                    string env1 = new String(screenBuffer[9], 1, 7).Trim();
                    if(env1 == "ENV1 TO")
                    {
                        pageTitle += " Effects";
                    }
                }

                // Speak the page title
                synthesizer.SpeakAsync(pageTitle);
            }           

            // Extract the active row and column based on where the selection is.
            string row = new String(screenBuffer[selectionY], 1, 2).Trim();
            string column = new String(screenBuffer[5], selectionX, 3).Trim();
            bool isValue = false;

            // If we didn't find the column header, we might be editing the value of something
            // (such as the value of an FX1, FX2, etc. in the Phrase window. If so, find the column
            // name by looking backwards and remember that this is a value.
            if(String.IsNullOrEmpty(column))
            {
                int spaceIndex = new String(screenBuffer[5]).LastIndexOf(' ', selectionX - 1, selectionX);
                column = new String(screenBuffer[5], spaceIndex + 1, 3);
                isValue = true;
            }

            // Describe the Song page
            if (currentPage.StartsWith("SONG"))
            {
                synthesizer.SpeakAsync(selectionContents + ", at row " + row + " track " + column);

                // Would be great to speak navigation during an extended nav button press. The interface
                // is a bit too chatty if we leave this in, even with a pause.
                // synthesizer.SpeakAsync("Live Mode, Project Home, Chain Home, Mixer Home");
            }

            // Describe the Chain page
            if (currentPage.StartsWith("CHAIN"))
            {
                // Expand some acronyms
                Dictionary<string, string> columnMap = new Dictionary<string, string>()
                {
                    { "PH", "Phrase" },
                    { "TSP", "Transpose" }
                };
                synthesizer.SpeakAsync(selectionContents + ", at row " + row + " for " + columnMap[column]);

                // Would be great to speak navigation during an extended nav button press. The interface
                // is a bit too chatty if we leave this in, even with a pause.
                // synthesizer.SpeakAsync("Song Home, Project Home, Phrase Home, Mixer Home");
            }

            // Describe the Phrase page
            if (currentPage.StartsWith("PHRASE"))
            {
                // Expand some acronyms
                Dictionary<string, string> columnMap = new Dictionary<string, string>()
                {
                    { "N", "Note" },
                    { "V", "Velocity" },
                    { "I", "Instrument" },
                    { "FX1", "FX1" },
                    { "FX2", "FX2" },
                    { "FX3", "FX3" },
                };

                if (isValue)
                {
                    synthesizer.SpeakAsync(selectionContents + ", at row " + row + " for " + columnMap[column] + " value.");
                }
                else
                {
                    synthesizer.SpeakAsync(selectionContents + ", at row " + row + " for " + columnMap[column]);
                }

                // Would be great to speak navigation during an extended nav button press. The interface
                // is a bit too chatty if we leave this in, even with a pause.
                // synthesizer.SpeakAsync("Chain Home, Groove Home, Instrument Home, Mixer Home");
            }

            // Describe the Instrument page
            if (currentPage.StartsWith("INST."))
            {
                // Expand some acronyms
                Dictionary<string, string> columnMap = new Dictionary<string, string>()
                {
                    { "TRANSP.", "Transpose" },
                    { "RES", "Resonance" },
                    { "AMP", "Amplification" },
                    { "LIM", "Limit" },
                    { "CHO", "Chorus" },
                    { "DEL", "Delay" },
                    { "REV", "Reverb" },
                };

                // Instrument effects are described with a row header just to the left of the 
                // selection. Extract that.
                string rowHeader = new String(screenBuffer[selectionY]).Substring(
                    Math.Max(selectionX - 8, 0), 7).Trim();
                if (columnMap.ContainsKey(rowHeader))
                {
                    rowHeader = columnMap[rowHeader];
                }

                // There is no real header for the Load / Save, so just ignore that.
                if(selectionContents == "LOAD" || selectionContents == "SAVE")
                {
                    rowHeader = "";
                }

                if (selectionContents == "REC.")
                {
                    selectionContents = "Record";
                }

                // This one is aligned a bit weird, so fudge it.
                if ((selectionX == 28) && (selectionY == 7))
                {
                    rowHeader = "Table Tick";
                }

                // Speak the selection as "Value", then "Header". This lets you quickly navigate and change values
                // and hear what the current value is without having to wait for the spoken version to say the header first.
                synthesizer.SpeakAsync(selectionContents + ", " + rowHeader);

                // Would be great to speak navigation during an extended nav button press. The interface
                // is a bit too chatty if we leave this in, even with a pause.
                // synthesizer.SpeakAsync("Phrase Home, Effects Home, Table Home, Mixer Home");
            }

            // Describe the Table page
            if (currentPage.StartsWith("TABLE"))
            {
                // Expand some acronyms
                Dictionary<string, string> columnMap = new Dictionary<string, string>()
                {
                    { "N", "Note" },
                    { "V", "Velocity" },
                    { "FX1", "FX1" },
                    { "FX2", "FX2" },
                    { "FX3", "FX3" },
                };

                // Speak the selection as "Value", then "Descriptor". This lets you quickly navigate and change values
                // and hear what the current value is without having to wait for the spoken version to say the header first.
                if (isValue)
                {
                    synthesizer.SpeakAsync(selectionContents + ", at row " + row + " for " + columnMap[column] + " value.");
                }
                else
                {
                    synthesizer.SpeakAsync(selectionContents + ", at row " + row + " for " + columnMap[column]);
                }

                // Would be great to speak navigation during an extended nav button press. The interface
                // is a bit too chatty if we leave this in, even with a pause.
                // synthesizer.SpeakAsync("Instrument Home, Effects Home, No Action, Mixer Home");
            }

            // Describe the Project page
            if (currentPage.StartsWith("PROJECT"))
            {
                // Expand some acronyms
                Dictionary<string, string> columnMap = new Dictionary<string, string>()
                {
                    { "OUTPUT VOL", "Output Volume" },
                    { "SPEAKER VOL", "Speaker Volume" },
                };

                // Project settings are described with a row header just to the left of the  selection. Extract that.
                string rowHeader = new String(screenBuffer[selectionY]).Substring(Math.Max(selectionX - 15, 0), 14).Trim();
                if (columnMap.ContainsKey(rowHeader))
                {
                    rowHeader = columnMap[rowHeader];
                }

                // Special case the Load / Save / New selections
                if (selectionContents == "LOAD" || selectionContents == "SAVE" || selectionContents == "NEW")
                {
                    rowHeader = "Project";
                }

                // Special case the Render / Bundle selections
                if (selectionContents == "RENDER" || selectionContents == "BUNDLE")
                {
                    rowHeader = "Export and Share";
                }

                // Special case the Clean and Pack section
                if (selectionContents == "PHRASES+CHAINS" || selectionContents == "INSTRUMENTS+TABLES")
                {
                    rowHeader = "Clean and Pack";
                }

                // Speak the selection as "Value", then "Descriptor". This lets you quickly navigate and change values
                // and hear what the current value is without having to wait for the spoken version to say the header first.
                synthesizer.SpeakAsync(selectionContents + ", " + rowHeader);

                // Would be great to speak navigation during an extended nav button press. The interface
                // is a bit too chatty if we leave this in, even with a pause.
                // synthesizer.SpeakAsync("Song Home, No Action, Song Home, Song Home");
            }
        }

        // Extremely basic and fast function to do basic OCR on an image at given location for a specific character. Because we know the exact font
        // and exact image sizes, we don't have to do anything fancy. As soon as we find a pixel that doesn't match what we're expecting for a character,
        // we know it's not the right character and can just continue. Once we've gone through the whole 10x14 character grid and all pixels matched,
        // then we know what character it is.
        //
        // There is one minor trick this function uses, which is to process each character at two offsets (even and odd pixels). Many letters have
        // big runs of either horizontal or vertical pixels ("I", "T", etc.), so doing it this way reduces the average number of pixel tests
        // down to examining about 3 pixels per character.
        private static void TestCharacterPixels(int init, int[,,] fontPixelData, ref int tests, System.Drawing.Imaging.BitmapData bmpData,
            byte[] toBeRecognizedData, int selectionX, int selectionY, int bestSelectionLength, int imageY, int imageX,
            int currentChar, ref int pixelMismatches, ref int avgRed, ref int avgGreen, ref int avgBlue, ref int pixelsProcessed)
        {
            for (int y = init; y < 14 - init; y += 2)
            {
                for (int x = init; x < 10 - init; x += 2)
                {
                    tests++;

                    int fontPixelValue = fontPixelData[currentChar, y, x];

                    int offset = bmpData.Stride * (imageY + y) + (4 * (imageX + x));
                    int imgPixelRed = toBeRecognizedData[offset + 2];
                    int imgPixelGreen = toBeRecognizedData[offset + 1];
                    int imgPixelBlue = toBeRecognizedData[offset];

                    int imgPixelValue = imgPixelRed + imgPixelBlue + imgPixelGreen;
                    avgRed += imgPixelRed;
                    avgGreen += imgPixelGreen;
                    avgBlue += imgPixelBlue;
                    pixelsProcessed++;

                    if (imgPixelValue > 0) { imgPixelValue = 1; }

                    // If this was a selection, invert
                    if (
                        ((imageY + y) >= selectionY) &&
                        ((imageY + y) < (selectionY + 14)) &&
                        ((imageX + x) >= selectionX) &&
                        ((imageX + x) < (selectionX + bestSelectionLength)))
                    {
                        imgPixelValue = 1 - imgPixelValue;
                    }

                    // If the character at this pixel position doesn't match up exactly with what the image had at that
                    // position, we can stop comparing.
                    pixelMismatches += fontPixelValue ^ imgPixelValue;
                    if (pixelMismatches > 1) { break; }
                }

                if (pixelMismatches > 1) { break; }
            }
        }
    }
}