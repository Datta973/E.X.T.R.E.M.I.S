using System;

using System.Windows;
using System.Drawing;
using System.Windows.Input;
using System.Text.RegularExpressions;
using System.Diagnostics;

//using java.io;
//using java.util;
using edu.stanford.nlp.ling;
using edu.stanford.nlp.tagger.maxent;
using System.Collections.Generic;
using static Osion.Lib;


namespace Osion
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        private static MaxentTagger tagger;
        IDictionary<string, string[]> cfg = new Dictionary<string, string[]>()
                                            {

                                               {"S"   , new string[]{"VP"      } },
                                               {"VP"  , new string[]{"VP1 NP"} },
                                               {"VP1" , new string[]{"vb","jj"} },
                                               {"NP"  , new string[]{"NP1","dt NP1",""} },
                                               {"NP1" , new string[]{"nn","nnp","nns"  } }

                                            };

        Stack<DEF> nonterminals = new Stack<DEF>();
        Stack<string> terminals = new Stack<string>();
        Queue<string> uris = new Queue<string>();
        IDictionary<string, Function<DEF, DEF>> functions = new Dictionary<string, Function<DEF, DEF>>();

        Queue<string> inputQueue = new Queue<string>();

        private static string[] tokens;
        private static string[] lexemes;
        private static int index;
        private static bool error;
        private static string l;
        private static string RESULT_CODE;
        private static ProcessStartInfo startInfo;
        private static Process process;
        private static string USER_DIR = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);



        public MainWindow()
        {
            InitializeComponent();

            Server server = new Server();
            server.onMessage(delegate (string message)
            {
                Application.Current.Dispatcher.Invoke(delegate
                {
                    processSentence(message);
                });
            });
            server.Start();

            //new Popup("Dude ! that's an error ?!", "Error").Show();
            //return;

            // initializing process
            startInfo = new ProcessStartInfo();
            process = new Process();

            startInfo.WorkingDirectory = USER_DIR;
            process.StartInfo = startInfo;


            System.Windows.Forms.ContextMenu contextMenu = new System.Windows.Forms.ContextMenu();
            System.Windows.Forms.MenuItem menuItem = new System.Windows.Forms.MenuItem();
            contextMenu.MenuItems.AddRange(
                    new System.Windows.Forms.MenuItem[] { menuItem });

            menuItem.Index = 0;
            menuItem.Text = "Exit";
            menuItem.Click += delegate (object sender, EventArgs e)
            {
                this.Close();
            };

            System.Windows.Forms.NotifyIcon ni = new System.Windows.Forms.NotifyIcon();
            ni.Icon = new System.Drawing.Icon(@"..\..\Resources\oston.ico");
            ni.ContextMenu = contextMenu;
            ni.Visible = true;
            ni.Click += delegate (object sender, EventArgs args)
            {
                if (this.WindowState == WindowState.Minimized)
                {
                    this.Show();
                    this.WindowState = WindowState.Normal;
                    animate(this, OpacityProperty, 0, 1, 200);
                }

            };
            initialize();
        }

        protected override void OnStateChanged(EventArgs e)
        {
            /* if(WindowState == WindowState.Minimized)
             {
                 this.Hide();
             }
             base.OnStateChanged(e);*/
        }

        private void processSentence(string str)
        {
            

            var quotedList = Regex.Matches(str, "\"(.*?)\"");

            for (int i = 0; i < quotedList.Count; i++)
            {
                uris.Enqueue(quotedList[i].ToString().Trim('"'));
                str = str.Replace(quotedList[i].ToString(), "<URI>");
            }

            var input = str;
            var words = input.Split(' ');

            for (int i = 0; i < words.Length; i++)
            {

                if (isURL(words[i]) || isPATH(words[i]))

                {
                    uris.Enqueue(words[i]);
                    words[i] = "<URI>";
                }
                //Console.WriteLine(words[i]);
            }

            var uriProcessedString = string.Join(" ", words);

            var sentences = MaxentTagger.tokenizeText(new java.io.StringReader(uriProcessedString)).toArray();
            foreach (java.util.ArrayList sentence in sentences)
            {
                //Console.WriteLine(sentence);
                var taggedSentence = tagger.tagSentence(sentence);
                var tags = new string[taggedSentence.size()];
                lexemes = new string[taggedSentence.size()];
                var iterator = taggedSentence.listIterator();
                var i = 0;
                while (iterator.hasNext())
                {
                    var tmp = iterator.next().ToString().Split('/');
                    if (tmp[0] == "<URI>")
                    {
                        try
                        {
                            lexemes[i] = uris.Dequeue();
                        }
                        catch (Exception ex)
                        {

                        }

                    }
                    else if (tmp[0] == "=")
                    {
                        tmp[1] = "=";
                        lexemes[i] = tmp[0];
                    }
                    else
                    {
                        lexemes[i] = tmp[0];
                    }
                    tags[i] = tmp[1];
                    i++;
                }
                //Console.WriteLine(SentenceUtils.listToString(taggedSentence, false));
                var inputString = string.Join(" ", tags).ToLower();
                Console.WriteLine(inputString);
                parse(inputString);
                //parse(textBox.Text);
            }
        }
        private void execute(string command)
        {
            var list = command.Split(new[] { ' ' }, 2);
            //Console.WriteLine(list[0]);
            startInfo.FileName = list[0];
            if (list.Length != 1)
            {
                startInfo.Arguments = list[1];
            }
            process.Start();
        }

        private void initialize()
        {
            foreach (var key in cfg)
            {
                foreach (var product in key.Value)
                {
                    functions[key.Key + ":" + product] = delegate (DEF nt)
                    {
                        foreach (var item in product.Split(' '))
                        {
                            if (item.Length != 0 && isNonTerminal(item))
                            {
                                nonterminals.Pop();
                            }
                            else if (item.Length != 0)
                            {
                                terminals.Pop();
                            }
                        }
                        return nt;
                    };
                }
            }

            functions["NP1:nnp"] = delegate (DEF NP1)
            {
                var nnp = terminals.Pop();
                ///////
                NP1.value = nnp;
                ///////

                return NP1;
            };

            functions["NP1:nns"] = delegate (DEF NP1)
            {
                var nns = terminals.Pop();
                ///////
                NP1.value = nns;
                ///////

                return NP1;
            };

            functions["NP1:nn"] = delegate (DEF NP1)
            {
                var nn = terminals.Pop();
                ///////
                NP1.value = nn;
                ///////

                return NP1;
            };

            functions["NP:NP1"] = delegate (DEF NP)
            {
                var NP1 = nonterminals.Pop();

                //////////////

                NP.value = NP1.value;
                if (isURL(NP1.value))
                {
                    if (!NP1.value.StartsWith("http://") && !NP1.value.StartsWith("https://"))
                        NP.value = "http://" + NP1.value;
                    NP.type = "URL";
                }
                else
                    NP.type = "";
                /////////////

                return NP;
            };

            functions["NP:dt NP1"] = delegate (DEF NP)
            {
                var NP1 = nonterminals.Pop();
                terminals.Pop();
                //////////////

                NP.value = NP1.value;
                if (isURL(NP1.value))
                {
                    if (!NP1.value.StartsWith("http://") && !NP1.value.StartsWith("https://"))
                        NP.value = "http://" + NP1.value;
                    NP.type = "URL";
                }
                else
                    NP.type = "";
                /////////////

                return NP;
            };

            functions["NP:"] = delegate (DEF NP)
            {

                /////////////
                NP.type = "NULL";
                /////////////

                return NP;
            };


            functions["VP1:jj"] = delegate (DEF VP1)
            {
                var jj = terminals.Pop();
                ///////
                VP1.value = jj;
                ///////

                return VP1;
            };

            functions["VP1:vb"] = delegate (DEF VP1)
            {
                var vb = terminals.Pop();
                ///////
                VP1.value = vb;
                ///////

                return VP1;
            };

            functions["VP:VP1 NP"] = delegate (DEF VP)
            {
                var NP = nonterminals.Pop();
                var VP1 = nonterminals.Pop();

                ///////

                if (NP.type == "NULL")
                    VP.code = "direct command : " + VP1.value;
                else if (VP1.value == "open" || VP1.value == "start" || VP1.value == "run" || VP1.value == "execute")
                {
                    VP.code = NP.value;
                }

                ///////

                return VP;
            };

            functions["S:VP"] = delegate (DEF S)
            {
                var VP = nonterminals.Pop();

                RESULT_CODE = VP.code;
                ///////

                return S;
            };


            var str = @" set var = ""g:\j.a.r.v.i.s\hello there my friend\yes\g\file.exe"" and ""http://www.google.com"" ";
            //Console.WriteLine(str.Replace(Regex.Match(str, "\"(.*?)\"").ToString(),"<URI>"));

            //var str = @" set var = ""g:\j.a.r.v.i.s\hello there my friend\yes\g\file.exe"" and yeah ";
            //Console.WriteLine(str.Replace(Regex.Match(str, "\"(.*?)\"").ToString(), "<URI>"));



            var model = @"../../../../stanford_pos_tagger_data/models/" + "english-bidirectional-distsim.tagger";
            //Console.WriteLine(System.AppContext.BaseDirectory);
            if (!System.IO.File.Exists(model))
                throw new Exception($"Check path to the model file '{model}'");
            tagger = new MaxentTagger(model);

        }



        private void parse(string input)
        {
            Console.WriteLine("input : " + input);
            tokens = (input + " $").Split(' ');

            index = 0;
            error = false;
            l = nexttoken();

            try
            {
                evaluate("S");
            }catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
                error = true;
            }

            if (l == "$" && !error)
            {
                Console.WriteLine("Parse Success");
                try
                {
                    execute(RESULT_CODE);
                }
                catch (Exception ex)
                {
                    //MessageBox.Show(ex.Message);
                    new Popup(ex.Message, "Error").Show();
                }
            }
            else
            {
                new Popup("Sorry, What ?!", "Error").Show();
                Console.WriteLine("Parse Error");
            }

        }

        private string evaluate(string NT)
        {


            foreach (var production in cfg[NT])
            {
                var prod = production.Split(' ');
                if (production.Length == 0)
                {


                    //semantic rules for NT → ϵ
                    var LNT = functions[NT + ":" + production](new DEF());
                    nonterminals.Push(LNT);

                    Console.WriteLine(NT + " -> epsilon ");

                    return "EPSILON";


                }
                else if (isNonTerminal(prod[0]))
                {

                    var result = evaluate(prod[0]);
                    if (result == "INVALID")
                    {
                        goto next;
                    }
                    for (var i = 1; i < prod.Length; i++)
                    {
                        if (isNonTerminal(prod[i]))
                        {
                            evaluate(prod[i]);
                        }
                        else if (prod[i] == l)
                        {
                            terminals.Push(nextlex());
                            l = nexttoken();
                        }
                        else
                        {
                            if (result == "EPSILON")
                            {
                                goto next;
                            }
                            else
                            {
                                error = true;

                                return "INVALID";
                            }

                        }
                    }


                    //semantic rules for NT → A                                       


                    var LNT = functions[NT + ":" + production](new DEF());
                    nonterminals.Push(LNT);

                    Console.WriteLine(NT + " -> " + string.Join(" ", prod));

                    return "NON_TERMINAL";
                }
                else if (prod[0] == l)
                {
                    terminals.Push(nextlex());
                    l = nexttoken();
                    for (var i = 1; i < prod.Length; i++)
                    {
                        if (isNonTerminal(prod[i]))
                            evaluate(prod[i]);
                        else if (prod[i] == l)
                        {
                            //Console.WriteLine("matched", l);
                            terminals.Push(nextlex());
                            l = nexttoken();
                        }
                        else
                            error = true;
                    }

                    //semantic rules for NT → a
                    var LNT = functions[NT + ":" + production](new DEF());
                    nonterminals.Push(LNT);
                    Console.WriteLine(NT + " -> " + string.Join(" ", prod));
                    return "TERMINAL";
                }
            next:;
            }
            return "INVALID";
        }

        private bool isNonTerminal(string str)
        {
            return str != str.ToLower();
        }

        private string nexttoken()
        {
            return tokens[index++];
        }

        private string nextlex()
        {
            return lexemes[index - 1];
        }

        private bool isURL(string url)
        {
            return Regex.IsMatch(url.ToLower(), @"^(http:\/\/www\.|https:\/\/www\.|http:\/\/|https:\/\/)?[a-z0-9]+([\-\.]{1}[a-z0-9]+)*\.[a-z]{2,5}(:[0-9]{1,5})?(\/[^\s]*)?$");
        }

        private bool isPATH(string path)
        {
            return Regex.IsMatch(path.ToLower(), @"^(?:[\w]\:\\)(([a-z_\.\-\s0-9]+[a-z_\-0-9]*\\?)+)?([a-z_\-\s0-9]+\.[a-z_\-\s0-9]+\\?)?$");
        }


        public delegate oup Function<in inp, out oup>(inp arg);

        private void dragWindow(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;
            //e.Handled = true;
            this.DragMove();
            //MessageBox.Show("click");
            Keyboard.ClearFocus();
        }

        private void textBox_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {

                Console.WriteLine("entered");
                processSentence(textBox.Text);

                //parse(input);
                textBox.SelectAll();
            }
        }

        private void Minimize_MouseEnter(object sender, MouseEventArgs e)
        {
            var converter = new System.Windows.Media.BrushConverter();
            var brush = (System.Windows.Media.Brush)converter.ConvertFromString("#FF3F3F41");

            minimize.Background = brush;


            //minimize.Background.Opacity = .5;
            //minimize.BorderBrush.Opacity = .5;
        }

        private void Minimize_MouseLeave(object sender, MouseEventArgs e)
        {
            var converter = new System.Windows.Media.BrushConverter();
            var brush = (System.Windows.Media.Brush)converter.ConvertFromString("#FF2D2D30");
            minimize.Background = brush;

            //minimize.Background.Opacity = 1;
            //minimize.BorderBrush.Opacity = 1;
        }




        private void textBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (textBox.Foreground == System.Windows.Media.Brushes.Gray)
            {
                textBox.Text = "";
                textBox.Foreground = System.Windows.Media.Brushes.Black;
            }
        }

        private void textBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (textBox.Text == "")
            {
                textBox.Text = "Type here...";
                textBox.Foreground = System.Windows.Media.Brushes.Gray;
            }
        }

        private void minimize_MouseUp(object sender, MouseButtonEventArgs e)
        {

            //this.WindowState = WindowState.Minimized;
            animate(this, OpacityProperty, 1, 0, 200, delegate (object obj, EventArgs args) {
                this.WindowState = WindowState.Minimized;
                this.Hide();
            });
        }

        private void minimize_MouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
        }



        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.Opacity = 0;
            animate(this, OpacityProperty, 0, 1, 1000);
        }
    }

    public class DEF
    {
        public string value;
        public string code;
        public string type;
        public DEF()
        {

        }
    }



}
