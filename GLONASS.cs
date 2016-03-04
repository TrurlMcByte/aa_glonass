using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Drawing;
using Jungler.Bot.Classes;
using System.Data.SQLite;

namespace Dwader.Navigation {

    /// <summary>
    /// Pause Ticket class
    /// </summary>
    public class PauseTicket : IDisposable {
        /// <summary>
        /// age of ticket
        /// </summary>
        public DateTime age { get; private set; }
        /// <summary>
        /// ticket disposed (expired)
        /// </summary>
        public bool disposed { get; private set; }
        /// <summary>
        /// constructor
        /// </summary>
        /// <param name="timer"></param>
        public PauseTicket(TimeSpan timer) {
            age = DateTime.Now + timer;
            disposed = false;
        }
        /// <summary>
        /// Dispose ticket (same as GLONASS.upPause)
        /// </summary><seealso cref="Dwader.Navigation.GLONASS.unPause(PauseTicket)"/>
        public void Dispose() {
            disposed = true;
            //Dispose();
        }
    }

    /// <summary>
    /// GLONASS error codes
    /// </summary>
    public enum GLONASSLastError {
        /// <summary>Not deffined</summary>
        None,
        /// <summary>Graph of routes isn't loaded</summary>
        GraphIsEmpty,
        /// <summary>Empty destination parametr</summary>
        DestinationIsEmpty,
        /// <summary>Too far from routes network, try to increase lazydist</summary>
        StartPointTooFar,
        /// <summary>Unknown destination</summary>
        DestinationNotFound,
        /// <summary>No way</summary>
        PathNotFound,
        /// <summary>Destination Point Too Far, try increase lazydist</summary>
        DestinationPointTooFar,
        /// <summary>Destination Too Close, we already here</summary>
        DestinationTooClose,
        /// <summary>Lost route after Pause (moved too far from route)</summary>
        TargetLost,
        /// <summary>Direct route</summary>
        DirectRoute

    }

    /// <summary>
    /// base navigation object
    /// </summary>
    public class GLONASS : IDisposable {

        #region public properties
        /// <summary>
        /// We currently moving?
        /// </summary>
        public bool isMoving { get { return this.automovestatus == 1 || this.automovestatus == 4 || this.automovestatus == 5 || this.automovestatus == 6; } }
        /// <summary>
        /// We currently on pause?
        /// </summary>
        public bool isOnPause { get { return this.automovestatus == 2 || this.automovestatus == 3; } }
        /// <summary>
        /// I have a diver down; keep well clear at slow speed.
        /// </summary>
        public bool ICS_Alpha { get { return this.automovestatus == 2 || this.automovestatus == 3; } }
        /// <summary>
        /// ICS Hotel
        /// Pilot on board
        /// ComeTo method move
        /// </summary>      
        public bool ICS_Hotel { get { return this.automovestatus == 6; } }
        /// <summary>
        /// I require a pilot.
        /// Switching to Pilot mode
        /// </summary><see cref="ICS_Hotel"/>
        public bool ICS_Golf { get { return this.automovestatus == 5; } set { if (this.automovestatus != 1 || this.path.Count == 0) return; if (value == true) { PilotMode = true; Dbg("Pilot on"); this.automovestatus = 5; } else { this.automovestatus = 4; PilotMode = false; Dbg("Pilot off"); } } }
        /// <summary>
        /// My vessel is stopped and making no way through the water
        /// Move finished
        /// Reactor in idle mode
        /// </summary>
        public bool ICS_Mike { get { return this.automovestatus == 2 && this.path.Count == 0; } }
        /// <summary>
        /// All persons should report on board as the vessel is about to proceed to sea.
        /// Starting move
        /// </summary>
        public bool ICS_Papa { get { return this.automovestatus == 3 || this.path.Count > 0; } }
        /// <summary>
        /// I am dragging my anchor.
        /// Stopping mode
        /// </summary>
        public bool ICS_Yankee { get { return this.automovestatus == 3; } }
        /// <summary>
        /// I am taking in or discharging or carrying dangerous goods.
        /// On Slave
        /// </summary>
        public bool ICS_Bravo { get { return this.onslave; } }
        /// <summary>
        /// I am operating astern propulsion.
        /// </summary>
        public bool ICS_Sierra { get { return this._core.moveBackwardState; } }
        /// <summary>
        /// I require medical assistance.
        /// </summary>
        public bool ICS_Whiskey { get { return this._core.me.hpp < 90; } }
        /// <summary>
        /// My vessel is 'healthy'
        /// </summary>
        public bool ICS_Quebec { get { return this._core.me.hpp == 100; } }
        /// <summary>
        /// I am disabled; communicate with me.
        /// </summary>
        public bool ICS_Foxtrot { get { return isMoving && !isOnPause && this.autorotatestatus == 2; } }
        /// <summary>
        /// Use only Pilot mode
        /// May be auto turned off, for example, on Slave
        /// </summary>
        public bool PilotMode { get; set; }
        /// <summary>
        /// GLONASS still working
        /// <code>
        /// using(GLONASS Nav = new GLONASS(this,mypathsdb)) {
        /// Nav.MoveTo("far point name");
        /// while(Nav.isWorking) Thread.Sleep(1000); }</code>
        /// </summary>
        public bool isWorking { get { return this.path.Count > 0; } }
        /// <summary>
        /// gps acts distance (if pack then half of this)
        /// default = 100
        /// <remarks>
        /// Отвечает за максимальную дистанцию.
        /// Дальше lazydist ему по прямой ехать лениво просто.
        /// Надетый на спину пак еще больше сокращает эту дистанцию (мало того что лень, так еще и тяжко) 
        /// Еще одно применение lazydist - дистанция от героя до ближайшего маршрута должна быть не больше lazydist/2. 
        /// И до конечной точки (если она не на маршруте, а такое GLONASS позволяет <see cref="Dwader.Navigation.GLONASS.MoveTo(Dwader.Navigation.Point3D,System.Double,System.Double,System.Action{Dwader.Navigation.Point3D},System.UInt16)"/>) - тоже lazydist/2 
        /// </remarks>
        /// </summary>
        public double lazydist = 100;
        /// <summary>
        /// current path, list of Point3D, readonly
        /// </summary>
        public List<Point3D> path { get; private set; }
        /// <summary>
        /// current status of moving
        /// <see cref="isWorking"/>
        /// <see cref="isMoving"/>
        /// <see cref="isOnPause"/>
        /// <remarks>
        /// все что больше 0 - работа продолжается, все что меньше - работа окончена.
        /// <list type="table">
        /// <listheader><term>State</term><description>description</description></listheader>
        /// <item><term>1</term><description>двигаемся по маршруту.</description></item>
        /// <item><term>2</term><description>состояние паузы</description></item>
        /// <item><term>3</term><description>переход в состояние паузы в данный момент (останавливается движение и повороты), по окончании - состояние 2</description></item>
        /// <item><term>4</term><description>возобновление движение, вызов onDoMount, по окончании - состояние 1</description></item>
        /// <item><term>5</term><description>переход/движение в лоцманском режиме</description></item>
        /// <item><term>6</term><description>движение в лоцманском режиме</description></item>
        /// <item><term>0</term><description>reserved</description></item>
        /// <item><term>меньше 0</term><description>полное отключение, ликвидация объекта GLONASS</description></item>
        /// </list>
        /// </remarks>
        /// </summary>
        public int automovestatus { get; private set; }
        /// <summary>
        /// last error code
        /// </summary>
        public GLONASSLastError lasterror { get; private set; }
        /// <summary>
        /// bred?
        /// </summary>
        public Dictionary<Point3D, Tuple<ushort, Action<Point3D>>> actions { get; set; }
        #endregion
        #region events
        /// <summary>
        /// on start and unpause event
        /// </summary><seealso cref="onDoMount"/>
        public event onDoMount onMount;
        /// <summary>
        /// on start and unpause event
        /// Предназначен для вызова/оседлания маунта при движении по маршруту
        /// </summary>
        /// <param name="dist">дистанция до конца маршрута (чтоб не вызывали лошадь ради 2х метров)</param>
        public delegate void onDoMount(double dist);
        /// <summary>
        /// buff cycle event
        /// Позволяет добавлять свои баффы для движения в общий цикл системы жизнеобеспечения 
        /// <remarks>Автоматом применяются "Отставить бунт", "Рывок", "Походный марш", "Бесхитростная мечта" и "Песнь исцеления" (если нужна). 
        /// Так же обновляется UpdateNoAfkState. 
        /// Морковку не жрет, если стоит галочка "collector" (для любителей постоянно соскакивать с ослика и собирать травки - иначе морковки не напасешься).
        /// </remarks>
        /// </summary><seealso cref="onDoBuff"/>
        public event onDoBuff onBuff;
        /// <summary>
        /// buff cycle event
        /// <see cref="GLONASS.onDoMount"/>
        /// </summary>
        /// <param name="dist">дистанция до конца маршрута</param>
        public delegate void onDoBuff(double dist);
        /// <summary>
        /// on every point event (deprecated)
        /// </summary><seealso cref="onDoPreMove"/>
        public event onDoPreMove onPreMove;
        /// <summary>
        /// on every point event (deprecated)
        /// </summary>
        /// <param name="next">дистанция до конца маршрута</param>
        public delegate void onDoPreMove(Point3D next);
        /// <summary>
        /// Скорость в направлении следующей точки (метров/цикл)
        /// <remarks>может отличаться от реальной скорости</remarks>
        /// </summary><seealso cref="speed"/>
        public double pathspeed { get; private set; }
        /// <summary>
        /// Реальная скрорость (метров/цикл)
        /// </summary>
        public double speed { get; private set; }
        /// <summary>
        /// дистанция до конца маршрута
        /// </summary>
        public double totaldist { get; private set; }
        /// <summary>
        /// we on slave (ship/tractor)
        /// </summary>
        public bool onslave { get; private set; }
        /// <summary>
        /// Включает вывод отладочной информации
        /// </summary>
        public bool DEBUG = false;
        #endregion
        #region deprecated properties
        /// <summary>
        /// Jungler.Bot.Classes.Gps object (used for precalculate route)
        /// Open public for back compatibility
        /// deprecated
        /// </summary>
        //public Jungler.Bot.Classes.Gps gps = null; // TODO Deprecate
        #endregion
        #region private properties
        private Jungler.Bot.Classes.Core _core = null;
        //private string destination = null;
        private int autorotatestatus = 0;
        private int autobuffstatus = 0;
        private Task automove = null;
        private Task autorotate = null;
        private Task autobuff = null;
        private List<PauseTicket> ptickets;
        private Random random = null;
        private Point3D ar_target = null;
        private DateTime lastturn = DateTime.Now;
        private double rotation = 0;
        private double rotateinstop = 0;
        private int automove_sleep = 100;
        private int autorotate_sleep = 50;
        private int an = 0;
        /// <summary>
        /// auto unpause on afk
        /// <remarks>Если true - в случае состояния isOnPause и AFKstate 
        /// - автоматически сбрасывает все тикеты и возобновлет движение</remarks>
        /// </summary><see cref="automovestatus"/>
        public bool unpauseonafk = true;
        /// <summary>
        /// max distance between control points
        /// default is 20
        /// <remarks>
        /// Для контроля движения ставятся опорные точки, за частоту которых отвечает dmax. 
        /// В ней указывается примерное расстояние между опорными точками. 
        /// То есть если туда вписать что-то типа 9999 - он их вообще перестанет использовать.
        /// Указывать надо ДО построения маршрута (то есть до вызова GpsMove/MoveTo).
        /// Изменения оной ПОСЛЕ получения маршрута тоже могут повлиять, 
        /// если вы поставите маршрут на паузу и куда-то уйдете с маршрута, 
        /// она будет применяться для построения маршрута "на маршрут".
        /// </remarks>
        /// </summary>
        public int dmax;
        /// <summary>
        /// next point
        /// </summary>
        public Point3D next { get; private set; }
        /// <summary>
        /// next next point (for triangle)
        /// </summary>
        public Point3D next2 { get; private set; }
        /// <summary>
        /// prev point
        /// </summary>
        public Point3D prev { get; private set; }
        /// <summary>
        /// me or my mount or my slave as Point3D object
        /// </summary>
        public Point3D me { get; private set; }
        //private double mr = 0;
        /// <summary>
        /// Graph of routes in native mode
        /// </summary>
        public Graph G { get; set; }
        /// <summary>
        /// AStar path resolver in native mode
        /// <seealso cref="AStar.ChoosenHeuristic"/>
        /// <seealso cref="AStar.DijkstraHeuristicBalance"/>
        /// </summary>
        public AStar AS { get; set; }
        #endregion
        #region Constructor
        /// <summary>
        /// Constructor GLONASS
        /// </summary>
        /// <param name="core">Core object (базовый объект плугина)</param>
        /// <param name="dbpath">gps database file</param>
        /// <param name="inDEBUG">debugging mode</param>
        /// <seealso cref="Jungler.Bot.Classes.Core" /><seealso cref="Jungler.Bot.Classes.Gps"/>
        public GLONASS(Jungler.Bot.Classes.Core core, string dbpath = null, bool inDEBUG = false) {
            this.DEBUG = inDEBUG;
            //AppDomain currentDomain = AppDomain.CurrentDomain;
            //currentDomain.AssemblyResolve += new ResolveEventHandler(CurrentDomain_AssemblyResolve);
            if (automove_sleep < core.pingToServer) automove_sleep = 50;
            this._core = core;
            this.dmax = 20;
            this.ptickets = new List<PauseTicket> { };
            lasterror = GLONASSLastError.None;
            this.random = new Random(System.DateTime.Now.Millisecond);
            try {
                //this.G = Graph.LoadFromFile(dbpath);
                this.G = new Graph { };
                if (dbpath != null) this.LoadFromDb(dbpath);
                Dbg("Loaded " + G.Nodes.Count + " nodes, " + G.Arcs.Count + " arcs.");
            } catch (Exception ing) {
                Err("Graph not loaded: " + ing.Message);
                this.G = new Graph { };

            }
            SetMe();
            next = null;
            next2 = null;
            prev = null;
            this.actions = new Dictionary<Point3D, Tuple<ushort, Action<Point3D>>> { };
            this.path = new List<Point3D> { };
            this.AS = new AStar(this.G);
            AS.ChoosenHeuristic = AStar.ManhattanHeuristic;
            AS.DijkstraHeuristicBalance = 0.5;
            this.automovestatus = 2;
            this.autorotatestatus = 2;
            this.autobuffstatus = 2;
            TaskStart(ref this.automove, this.AutoMoving);
            TaskStart(ref this.autorotate, this.AutoRotation);
            TaskStart(ref this.autobuff, this.MovingBuffs);
            Thread.Sleep(50);
            //            
        }
        #endregion
        #region GpsMove
        /// <summary>
        /// Start move (blocking)
        /// Двигается по маршруту. Управление возвращается после окончания движения.
        /// </summary>
        /// <param name="name">dest point name</param>
        /// <returns>true on any finish, false if no route</returns>
        public bool GpsMove2(string name) {
            lasterror = GLONASSLastError.None;
            if (!GpsMove(name)) return false;
            Thread.Sleep(100);
            try {
                while (this.isWorking) Thread.Sleep(1000);
            } catch (ThreadAbortException) { return false; }
            return true;
        }
        /// <summary>
        /// Start move (NON BLOCKING!!!)
        /// Двигается по маршруту. Управление возвращается после начала движения.
        /// </summary>
        /// <seealso cref="onDoMount"/>
        /// <param name="destination">dest point name</param>
        /// <returns>false if no route</returns>
        public bool GpsMove(string destination) {
            lasterror = GLONASSLastError.None;
            if (destination == null || destination == "") {
                lasterror = GLONASSLastError.DestinationIsEmpty;
                return false;
            }
            var endnode = this.G.NodeByName(destination);
            if (endnode == null) {
                lasterror = GLONASSLastError.DestinationNotFound;
                return false;
            }
            return GpsMove(endnode.Position);
        }
        /// <summary>
        /// overload
        /// </summary><see cref="GpsMove(string)"/>
        /// <param name="destination"></param>
        /// <returns></returns>
        public bool MoveTo(string destination) { return GpsMove(destination); }
        /// <summary>
        /// GpsMove to point
        /// ATTN! if path can't me generated will go directly to point!
        /// </summary>
        /// <param name="point">target point (also Creature)</param>
        /// <returns></returns>
        public bool GpsMove(Point3D point) { return MoveTo(point); }
        /// <summary>
        /// Move to point/Creature/Doodad
        /// start point is last current point or Self if current path is empty
        /// </summary>
        /// <param name="point">destination point</param>
        /// <param name="dist1">distnance from start point to grapth, default (-1) = lazydist/2</param>
        /// <param name="dist2">distnance from grapth to destination, default (-1) = lazydist/2</param>
        /// <param name="act">Action on destination</param>
        /// <param name="type">action type, default 0. Bitmask! 2 - stop moving before, 4 - call doMount after</param>
        /// <returns>true if have route</returns>
        public bool MoveTo(Point3D point, double dist1 = -1, double dist2 = -1, Action<Point3D> act = null, ushort type = 0) {
            lasterror = GLONASSLastError.None;
            if (point == null) {
                lasterror = GLONASSLastError.DestinationIsEmpty;
                return false;
            }
            if (dist1 == -1) dist1 = lazydist / 2;
            if (dist2 == -1) dist2 = lazydist / 2;

            var newpath = this.GeneratePath(point, null, dist1, dist2);
            if (newpath.Count == 0) return false;
            //    foreach (var pp in newpath) _core.Log("@ "+pp + " dist="+me.distZ(pp));
            //    return false;
            if (act != null) actions.Add(newpath[newpath.Count - 1], Tuple.Create(type, act));
            this.path.AddRange(newpath);
            this.totaldist = this.path.Sum(x => x.ndist);

            //Dbg("set automovestatus = 4 - start");
            //this.automovestatus = 4;
            Thread.Sleep(250);
            return true;
        }
        /*
        /// <summary>
        /// Sub move inside main move
        /// </summary>
        /// <param name="newpoint">left point</param>
        /// <param name="act">act on this point</param>
        /// <param name="type">type mask of act</param>
        /// <returns></returns>
        public bool SubMove(Point3D newpoint, Action<Point3D> act, ushort type = 0) {
            if (newpoint == null) {
                lasterror = GLONASSLastError.DestinationIsEmpty;
                return false;
            }
            if (next != null && next.distZ(newpoint) < (next.radius + newpoint.radius + 1)) {
                actions.Add(next, Tuple.Create(type, act)); return true;
            }
            var newpath = this.GeneratePath(newpoint, me);

            //Dbg("new subroute to " + newpoint + " cnt:" + newpath.Count + " last is " + newpath[newpath.Count - 1]);

            //if (newpath.Count > 0) newpath.RemoveAt(0); // TODO??
            Dbg("subpath count=" + newpath.Count);
            if (newpath.Count == 0) return false;
            var i = 0;
            for (; i <= newpath.Count; i++) {
                if (newpath[i] == this.path[i] && newpath.Count > 0 && this.path.Count > i) newpath.RemoveAt(0);
                else break;
            }
            if (newpath.Count > 0) {
                this.path.InsertRange(i, newpath);
            }
            Dbg("Merged at " + i);
            actions.Add(next, Tuple.Create(type, act)); // TODO check 

            if (newpath.Count > 0 && this.path.Count > i) {
                if (this.path.Count > (i + 1)) i++;
                var wayback = this.GeneratePath(this.path[i], newpoint);
                if (newpath.Count == 0 || wayback.Count == 0) return false; // no way back?
                wayback.RemoveAt(wayback.Count - 1);
                wayback.RemoveAt(0);
                this.path.InsertRange(i, wayback);
            }
            return true;
        }
        */
        /// <summary>
        /// Remove action from point
        /// </summary>
        /// <param name="point"></param>
        public void RemoveAction(Point3D point) { if (actions.ContainsKey(point)) actions.Remove(point); }
        /// <summary>
        /// GpsMove by own list of points
        /// </summary>
        /// <param name="newpath">list of Point3D, last point must have name property to use it as destination</param>
        /// <returns>false if less 2 points</returns>
        public bool GpsMove(List<Point3D> newpath) {
            if (newpath.Count() < 1) {
                lasterror = GLONASSLastError.DestinationTooClose;
                return false;
            }
            LoadPath(newpath);
            return GpsMove(newpath.LastOrDefault());
        }
        #endregion
        #region Load and init
        private System.Reflection.Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args) {
            System.Reflection.Assembly[] asms = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < asms.Length; ++i) {
                if (asms[i].FullName == args.Name)
                    return asms[i];
            }
            return null;
        }

        /// <summary>
        /// reload db routes
        /// </summary>
        /// <param name="dbpath">string of path to gps database</param>
        public void LoadDataBase(string dbpath) {
            try {
                G = new Graph { };  // or do merge?
                this.LoadFromDb(dbpath);
            } catch (Exception eeee) { Err("Can't load paths " + eeee.Message); }
        }
        /// <summary>
        /// Direct load path
        /// </summary>
        /// <param name="data">list of points</param>
        public void LoadPath(List<Point3D> data) {
            Node n = null;
            Node p = null;
            for (var ip = 0; ip < data.Count; ip++) {
                if (n != null) p = n;
                n = new Node(data[ip]);
                G.AddNode(n);
                if (p != null) G.AddArc(new Arc(p, n));
            }
        }

        private void LoadFromDb(string inputFile) {

            //_core.Log("test1=" + inputFile);
            Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
            Dictionary<int, Node> npoints = new Dictionary<int, Node> { };
            using (var cnn = new SQLiteConnection(String.Format("Data Source={0}; Version=3;Read Only=True;", inputFile))) {
                try {
                    cnn.Open();
                    var sql = "SELECT id, x, y, z, name, radius FROM point"; // WHERE temp_point = 0";
                    SQLiteCommand cmd = new SQLiteCommand(sql, cnn);
                    SQLiteDataReader reader = cmd.ExecuteReader();
                    while (reader.Read()) {
                        int id = 0; if (!Int32.TryParse(reader["id"].ToString(), out id)) continue;
                        if (npoints.ContainsKey(id)) continue;
                        double x = 0; if (!Double.TryParse(reader["x"].ToString(), out x)) continue;
                        double y = 0; if (!Double.TryParse(reader["y"].ToString(), out y)) continue;
                        double z = 0; if (!Double.TryParse(reader["z"].ToString(), out z)) continue;
                        string name = reader["name"].ToString();
                        double r = 0; if (!Double.TryParse(reader["radius"].ToString(), out r)) continue;
                        double dist = 0.02;
                        Node nd = G.ClosestNode(x, y, z, out dist, true);
                        if (dist > 0.02 || dist == -1) { nd = new Node(x, y, z); G.AddNode(nd); }
                        npoints.Add(id, nd);
                        if ((nd.name == null || nd.name == "") && name != null && name != "") nd.name = name;
                        if (nd.Position.radius < r) nd.Position.radius = r;
                    }
                    sql = "SELECT start_point_id, end_point_id, one_way FROM link"; // WHERE temp_point=0";
                    cmd = new SQLiteCommand(sql, cnn);
                    reader = cmd.ExecuteReader();
                    while (reader.Read()) {
                        int s_id = 0; if (!Int32.TryParse(reader["start_point_id"].ToString(), out s_id)) continue;
                        int e_id = 0; if (!Int32.TryParse(reader["end_point_id"].ToString(), out e_id)) continue;
                        if (e_id == s_id) continue; // ага, и такое бывает.                    
                        int ow = 0; if (!Int32.TryParse(reader["one_way"].ToString(), out ow)) continue;
                        if (!npoints.ContainsKey(s_id) || !npoints.ContainsKey(e_id)) continue;
                        if (npoints[s_id] == npoints[e_id]) continue;
                        try {
                            if (ow == 0) G.Add2Arcs(npoints[s_id], npoints[e_id], 1d);
                            else G.AddArc(npoints[s_id], npoints[e_id], 1d);
                        } catch (Exception add) { Err("add " + add.Message + "\n" + npoints[s_id]); }
                    }
                } catch (Exception e) { Err("dbload for \"" + inputFile + "\" error:\n" + e); }
            }
        }
        #endregion
        #region Moving Controls

        /// <summary>
        /// pause move
        /// </summary>
        public PauseTicket Pause(double tm = -1) {
            Dbg("pause " + this.automovestatus);
            if (this.automovestatus < 1) return null;
            try {
                TimeSpan timer;
                if (tm < 0) timer = TimeSpan.FromDays(100); // MaxValue низзя!
                else timer = TimeSpan.FromMilliseconds(tm);
                PauseTicket ticket = new PauseTicket(timer);
                ptickets.Add(ticket);


                Dbg("ptickets " + ptickets.Count);
                if (this.automovestatus == 1 || this.automovestatus == 4) {
                    this.automovestatus = 3; this.autorotatestatus = 3;
                }
                StopMoving();
                StopRotate();
                this.autobuffstatus = 2;
                // ToDo: save point for long dist
                Thread.Sleep(50);
                return ticket;
            } catch (Exception e) {
                Dbg("pause fail " + e);
                return null;
            }
        }
        /// <summary>
        /// stop (finish) move
        /// </summary>
        public void Stop() {
            if (this.automovestatus < 0) return; // ты о чем? 
            this.automovestatus = 2; this.autorotatestatus = 2; this.autobuffstatus = 2;
            this.path.Clear();
            ptickets.Clear();
            actions.Clear();
            StopMoving();
            StopRotate();
            next = null;
        }
        /// <summary>
        /// unpause move (after Pause only)
        /// </summary><see cref="Pause"/>
        public void unPause(PauseTicket ticket) {
            try {
                if (ticket != null) ticket.Dispose();
            } catch (ObjectDisposedException de) { Dbg("ticket already disposed" + de); }
            Dbg("unpause " + this.automovestatus + " lvl=" + ptickets.Count);
        }

        private bool CheckTickets() {
            try {
                if (ptickets.Count > 0) {
                    for (int ti = ptickets.Count - 1; ti >= 0; ti--)
                        try {
                            if (ptickets[ti] == null) ptickets.RemoveAt(ti);
                            else if (ptickets[ti].age < DateTime.Now)
                                ptickets[ti].Dispose();
                            if (ptickets[ti].disposed)
                                ptickets.RemoveAt(ti);
                        } catch (ObjectDisposedException) { ptickets.RemoveAt(ti); } // на свечку тоже
                }
            } catch (Exception e) { Err("CheckTickets " + e); }
            return (ptickets.Count > 0);
        }
        #endregion
        #region Prepare path
        /// <summary>
        /// Build route based on project any points to GLONASS arcs
        /// </summary>
        /// <param name="point1">First point (will be not included in path)</param>
        /// <param name="point2">Destinations point</param>
        /// <param name="mdist1">max distance from start point to GLONASS route</param>
        /// <param name="mdist2">max distance from destitation to GLONASS route</param>
        /// <param name="radius">radius of interception (projective) points</param>
        /// <returns>List of path points, if route fail list with only one destination point (for direct run)</returns>
        public List<Point3D> GetArcSplitRoute(Point3D point1, Point3D point2, double mdist1, double mdist2, double radius) {
            bool pass = true;
            var rdist1 = mdist1; var rdist2 = mdist2;
            var rndist1 = mdist1; var rndist2 = mdist2;
            var node1 = this.G.ClosestNode(point1, out rndist1, pass);
            var node2 = this.G.ClosestNode(point2, out rndist2, pass);
            var arc1 = this.G.ClosestArc(point1, out rdist1, pass);    // TODO optimize
            var arc2 = this.G.ClosestArc(point2, out rdist2, pass);
            //            if (node1 == node2) {
            //                lasterror = GLONASSLastError.DirectRoute;
            //                return new List<Point3D> { point2 };
            //            }
            //            if (arc1 == arc2) {
            //                lasterror = GLONASSLastError.DirectRoute;
            //                return new List<Point3D> { point2 };
            //            }

            if (rdist1 == -1 || rdist2 == -1 || rndist1 == -1 || rndist2 == -1) {
                lasterror = GLONASSLastError.GraphIsEmpty;
                return new List<Point3D> { point2 };
            }
            if (rdist1 > rndist1) rdist1 = rndist1; // node is closest
            if (rdist2 > rndist2) rdist2 = rndist2; // node is closest

            if (rdist1 > point1.distZ(point2) && rdist2 > point1.distZ(point2)) {
                Dbg("Direct route selected");
                return new List<Point3D> { point2 };
            }

            Point3D poststartpoint = null;
            Point3D preendpoint = null;
            if (rdist1 < rndist1) {
                poststartpoint = Point3D.ProjectOnLine(point1, arc1.StartNode.Position, arc1.EndNode.Position);
                poststartpoint.radius = radius;
                node1 = arc1.StartNode;
            }
            if (rdist2 < rndist2) {
                preendpoint = Point3D.ProjectOnLine(point2, arc2.StartNode.Position, arc2.EndNode.Position);
                preendpoint.radius = radius;
                node2 = arc2.StartNode;
            }

            if (poststartpoint != null) {
                if (point1.distZ(poststartpoint) > mdist1) {
                    Dbg("point1.distZ(poststartpoint) =" + point1.distZ(poststartpoint));
                    lasterror = GLONASSLastError.StartPointTooFar;
                    return new List<Point3D> { point2 };
                }
            } else if (point1.distZ(node1.Position) > mdist1) {
                Dbg("point1.distZ(node1.Position) =" + point1.distZ(node1.Position));
                lasterror = GLONASSLastError.StartPointTooFar;
                return new List<Point3D> { point2 };
            }

            if (preendpoint != null) {
                if (point2.distZ(preendpoint) > mdist2) {
                    Dbg("point2.distZ(preendpoint) =" + point2.distZ(preendpoint));
                    lasterror = GLONASSLastError.DestinationPointTooFar;
                    return new List<Point3D> { point2 };
                }
            } else if (point2.distZ(node2.Position) > mdist2) {
                Dbg("point2.distZ(node2.Position) =" + point2.distZ(node2.Position));
                lasterror = GLONASSLastError.DestinationPointTooFar;
                return new List<Point3D> { point2 };
            }

            if (!AS.SearchPath(node1, node2)) {
                lasterror = GLONASSLastError.PathNotFound;
                Dbg("SearchPath " + lasterror + " node1=" + node1 + " node2=" + node2);
                return new List<Point3D> { point2 };
            }

            //Dbg("poststartpoint=" + poststartpoint + " dist=" + me.distZ(poststartpoint));
            List<Point3D> npath = AS.PathByCoordinates.ToList();
            List<Point3D> path2 = new List<Point3D> { };

            var mustpoints = 2;
            if (rdist1 < rndist1) mustpoints++;
            if (rdist2 < rndist2) mustpoints++;
            if (npath.Count < mustpoints) {
                foreach (var np in npath) Dbg("p=" + np);
                //   Dbg("route to " + point2.name + " npath.Count=" + npath.Count + " mustpoints=" + mustpoints);                
                //  lasterror = GLONASSLastError.DestinationTooClose;
                //  return new List<Point3D> { point2 };
            }

            // block of removing unneed endpoints
            if (poststartpoint != null && npath.Count > 2 && ((npath[0] == arc1.StartNode.Position && npath[1] == arc1.EndNode.Position)
                    || (npath[1] == arc1.StartNode.Position && npath[0] == arc1.EndNode.Position))) {
                Dbg("start node by arc split");
                npath.RemoveAt(0);
            }
            if (preendpoint != null && npath.Count > 2 && ((npath[npath.Count() - 1] == arc2.StartNode.Position && npath[npath.Count() - 2] == arc2.EndNode.Position)
                || (npath[npath.Count() - 2] == arc2.StartNode.Position && npath[npath.Count() - 1] == arc2.EndNode.Position))) {
                Dbg("end node by arc split");
                npath.RemoveAt(npath.Count - 1);
            }
            // one madness off
            // adding split points and target                      

            if (poststartpoint != null) path2.Add(poststartpoint);
            path2.AddRange(npath);
            if (preendpoint != null) path2.Add(preendpoint);
            path2.Add(point2);
            Dbg("dists: " + rdist1 + " " + rdist2 + " " + rndist1 + " " + rndist2 + " Path count=" + path2.Count + " lpn=" + point2.name);
            return path2;
        }

        private List<Point3D> GeneratePath(Point3D destination, Point3D startpoint = null, double dist1 = -1, double dist2 = -1) {
            if (destination == null) {
                lasterror = GLONASSLastError.DestinationIsEmpty;
                return new List<Point3D> { };
            }
            if (dist1 == -1) dist1 = lazydist / 2;
            if (dist2 == -1) dist2 = lazydist / 2;
            if (startpoint == null) {
                startpoint = this.path.LastOrDefault();
                if (startpoint == null) {
                    SetMe();
                    startpoint = me;
                }
            }
            Dbg("GeneratePath from " + startpoint + " to " + destination + " current path:" + this.path.Count);
            if (startpoint.distNoZ(destination) < (startpoint.radius + destination.radius + 1)) {
                lasterror = GLONASSLastError.DestinationTooClose;
                return new List<Point3D> { };
            }
            if (G.Arcs.Count < 1) {
                lasterror = GLONASSLastError.GraphIsEmpty;
                return new List<Point3D> { };
            }
            //            Point3D c = _core.me;
            //double dist;
            //var startnode = this.G.ClosestNode(startpoint, out dist, true);
            //Dbg("ClosestNode dist=" + dist);
            //if (startnode == null) {
            //    lasterror = GLONASSLastError.GraphIsEmpty;
            //    return new List<Point3D> { };
            //}
            //if (dist > this.lazydist) {
            //    lasterror = GLONASSLastError.StartPointTooFar;
            //    return new List<Point3D> { };
            //}
            Dbg("destination = " + destination + "  startpoint = " + startpoint + " endpoint = " + destination);
            var newpath = GetArcSplitRoute(startpoint, destination, dist1, dist2, me.radius * 2);
            //Dbg("newpath.Count=" + newpath.Count);
            if (newpath.Count == 1 && startpoint.distZ(newpath[0]) > (dist1 + dist2)) {
                lasterror = GLONASSLastError.DestinationPointTooFar;
                return new List<Point3D> { };
            }
            if (newpath.Count == 1 && startpoint.distZ(newpath[newpath.Count - 1]) < (startpoint.radius + newpath[newpath.Count - 1].radius + 1)) {
                lasterror = GLONASSLastError.DestinationTooClose;
                return new List<Point3D> { };
            }
            //if (newpath.Count == 1) {
            //    return SplitPath(new List<Point3D> { destination });  // direct route
            //}
            //Dbg("AS.PathByCoordinates " + AS.PathByCoordinates.Count());
            return SplitPath(newpath);
        }
        /// <summary>
        ///  Рубит маршрут на короткие отрезки и прописывает длины
        /// </summary>
        /// <param name="path2">original path</param>
        /// <returns>splitted path</returns>
        public List<Point3D> SplitPath(List<Point3D> path2) {
            if (path2.Count < 2) return path2;
            Point3D c = path2[0];
            List<Point3D> rpath = new List<Point3D> { c };

            for (var i = 1; i < path2.Count; i++) {
                Point3D n = path2[i];
                if (c.distNoZ(n) < 0.1) continue; // paranoid mode off - skip duplicate
                //Log("@" + c.ToString() + " => " + n.ToString() + " dist=" + c.dist(n));
                double nd = c.distNoZ(n);
                while (nd > dmax) {
                    nd = c.distNoZ(n);
                    double d = Math.Round(nd / dmax, 0, MidpointRounding.AwayFromZero) - 1;
                    if (d <= n.radius) break;
                    double nx = Math.Round((c.X * d + n.X) / (d + 1), 2);
                    double ny = Math.Round((c.Y * d + n.Y) / (d + 1), 2);
                    c = new Point3D(nx, ny, _core.getZFromHeightMap(nx, ny), "");
                    c.ndist = nd / d;
                    //this.totaldist += c.ndist;
                    //Err("@@" + c + " => " + n + " dist=" + c.distZ(n) + " d=" + d);
                    rpath.Add(c);
                    //Dbg("@" + c);
                }
                c.ndist = c.distNoZ(n);
                //this.totaldist += c.ndist;
                c = n; rpath.Add(c);
            }
            //this.totaldist = path.Sum(x=> x.ndist);
            return rpath;
        }
        #endregion
        #region AutoRotation
        private void AutoRotation() {
            an = 0;
            double dist = 0;
            double rspd = 22.2; // 22.22 max
            //            Dbg("autorotation " + this.autorotatestatus);
            while (this.autorotatestatus > 0) {
                try {
                    Thread.Sleep(this.autorotate_sleep);
                    SetMe();
                    if (this.autorotatestatus == 3) { StopRotate(0); this.autorotatestatus = 2; Thread.Sleep(10); continue; }
                    if (this.autorotatestatus == 2) { Thread.Sleep(10); continue; }
                    if (this.autorotatestatus == 4) { this.autorotatestatus = 1; }
                    if (!isWorking) {
                        this.autorotatestatus = 3;
                        continue;
                    }

                    this.speed = Math.Sqrt(Math.Pow(_core.me.toX, 2) + Math.Pow(_core.me.toY, 2));
                    StopRotate(an);
                    if (this.ar_target == null) continue;
                    an = dean(_core.angle(_core.me, this.ar_target.X, this.ar_target.Y));
                    dist = me.distNoZ(this.ar_target);
                    if (Math.Abs(an) <= 2) an = 0;
                    if (Math.Abs(an) <= 5 && dist < 5) an = 0;

                    if (this.onslave) {
                        if (Math.Abs(an) > 3 && (Math.Abs(an) > ((dist / this.speed) * rspd) || Math.Abs(an) > 45 || dist < 4)) {
                            this.rotateinstop = 1; if (_core.moveForwardState) _core.MoveForward(false);
                            if (this.isWorking) this.automovestatus = 2;
                        }
                        //if (Math.Abs(an) > 1) Dbg("an=" + an + " turn=" + _core.me.turnAngle);                    
                        if (an < -3 && !_core.rotateLeftState) {
                            _core.RotateLeft(true);
                            if (Math.Abs(an) > 6) this.rotation = -1;
                            else {
                                Dbg("Fix Left"); Thread.Sleep(Math.Abs(an) * 22);
                                _core.RotateLeft(false);
                                //Dbg("Fix result1: was " + an + " now " + (dean(_core.angle(_core.me, this.ar_target.X, this.ar_target.Y))));
                                Thread.Sleep(200);
                                //Dbg("Fix result2: was " + an + " now " + (dean(_core.angle(_core.me, this.ar_target.X, this.ar_target.Y))));
                            }
                        } else if (an > 3 && !_core.rotateRightState) {
                            _core.RotateRight(true);
                            if (Math.Abs(an) > 6) this.rotation = 1;
                            else {
                                Dbg("Fix Right"); Thread.Sleep(Math.Abs(an) * 22);
                                _core.RotateRight(false);
                                Dbg("Fix result1: was " + an + " now " + (dean(_core.angle(_core.me, this.next.X, this.next.Y))));
                                Thread.Sleep(200);
                                Dbg("Fix result2: was " + an + " now " + (dean(_core.angle(_core.me, this.next.X, this.next.Y))));
                            }
                        }
                    } else {
                        // drift2
                        //Dbg("an=" + Math.Abs(an) + " rds="+((dist / this.speed) * rspd));
                        if (next != null && next2 != null && Math.Abs(an) > 50 && Math.Abs(an) > ((dist / this.speed) * rspd)) {
                            this.rotateinstop = 1; if (_core.moveForwardState) {
                                _core.MoveForward(false);
                                _core.MoveBackward(true); Thread.Sleep(50); _core.MoveBackward(false);
                            }
                            Dbg("EBD on");
                        }
                        HardTurn(an);
                        an = 0;
                        this.lastturn = DateTime.Now;
                    }
                    if (this.ar_target != null && dist <= this.ar_target.radius && Math.Abs(an) < 1) this.ar_target = null;
                } catch (ThreadAbortException) { break; } catch (Exception en) { Err("Steering gear faiure " + en); Thread.Sleep(500); }
            }
            StopRotate();
        }
        /// <summary>
        /// 360 to -180 - +180
        /// </summary>
        /// <param name="an">int degree</param>
        /// <returns>int degree</returns>
        public int dean(int an) { if (an > 180) return an - 360; else return an; }
        /// <summary>
        /// 360 to -180 - +180
        /// </summary>
        /// <param name="an">double degree</param>
        /// <returns>double degree</returns>
        public double dean(double an) { if (an > 180) return an - 360; else return an; }

        private void HardTurn(double an) { _core.Turn((Math.PI / -180) * an); }

        private void SmartTurn(double x, double y, double delta = 1d) {
            double man = -((Math.PI / 180) * dean(_core.angle(_core.me, x, y)));
            if (Math.Abs(man) > delta) {
                _core.Turn(man);
            }
        }

        private void StopRotate(double an = 0) {
            //if (an!=0) Dbg("stoprotate " + an + " r=" + this.rotation);
            if ((this.rotation < 0 || _core.rotateLeftState) && (an < -1 || this.autorotatestatus != 1)) { _core.RotateLeft(false); this.rotation = 0; }
            if ((this.rotation > 0 || _core.rotateRightState) && (an > 1 || this.autorotatestatus != 1)) { _core.RotateRight(false); this.rotation = 0; }
            //else return;
            if (this.rotateinstop > 0 && this.automovestatus == 2) {
                this.automovestatus = 1; if (!_core.moveForwardState) _core.MoveForward(true);
            }
            this.rotateinstop = 0;
        }

        #endregion
        #region AutoMoving
        private void AutoMoving() {
            int stuck = 0;
            double lastdist = 0;
            double na = 0;
            //if (mr == 0) mr = _core.me.modelRadius + 1;
            //if (_core.me.sitOnMount) mr = _core.me.sitMountObj.modelRadius + 1;
            //if (_core.me.sitOnMount) mr = 1.8;
            //if (!_core.me.sitOnMount) HardTurn(dean(_core.angle(_core.me, next.X, next.Y))); todo in unpause state            
            while (this.automovestatus > 0) {
                //Dbg("ams=" + this.automovestatus + " left=" + this.path.Count);
                try {

                    SetMe();

                    if (this.automovestatus == 5) {
                        #region PilotMode
                        Dbg("Pilot mode");
                        if (onslave) {
                            Err("Can't use pilot mode on slave");
                            this.automovestatus = 1;
                            PilotMode = false;
                            continue;
                        }
                        if (_core.moveForwardState) StopMoving();
                        StopRotate(); this.autorotatestatus = 2; this.autobuffstatus = 1;
                        if (next == null) { this.automovestatus = 2; continue; }
                        while (GetNextPoint() && PilotMode && isMoving) {
                            this.automovestatus = 6; Dbg("Pilot is" + PilotMode);
                            if (_core.ComeTo(next.X, next.Y, next.Z)) {
                                if (this.path.Count > 0) this.path.RemoveAt(0);
                                else break;
                            } else {
                                var lasterr = _core.GetLastError();
                                if (lasterr == LastError.MovePossibleFullStop) break;
                                Err("Pilot mode error: " + lasterr + ". Try to decrease lazydist and dmax");
                                this.automovestatus = 1;
                                break;
                            }
                        }
                        this.automovestatus = 2;
                        #endregion
                    }


                    Thread.Sleep(this.automove_sleep);

                    #region CheckNextPoint
                    // bit crazy                
                    bool nextpoint = false;
                    int skipcount = 5;
                    if (next != null && (isMoving || (onslave && autorotatestatus == 1)))
                        while (next != null && (me.distNoZ(next) <= next.radius + me.radius + speed * 0.2
                        ||
                        (next2 != null
                        && next.distNoZ(next2) >= (me.distNoZ(next2) + next2.radius + me.radius + speed * 0.2)))
                        ) {
                            this.ar_target = null;
                            if (skipcount-- < 0) break;
                            if (next != null && actions.ContainsKey(next)) try {
                                    Dbg("Act detected");
                                    if ((actions[next].Item1 & 2) != 0) {
                                        StopMoving(); StopRotate();
                                    }
                                    try {
                                        actions[next].Item2.Invoke(next);
                                    } catch (Exception acte) { Err("Error in action " + acte); }
                                    if ((actions[next].Item1 & 4) != 0) CallDoMount();
                                    actions.Remove(next); //?
                                } catch { Err("action failed on point " + next); }
                            if (this.path.Count < 1) {
                                Stop();
                                Dbg("destination point reached"); break;
                            } // приехали
                            this.path.RemoveAt(0);
                            this.totaldist -= next.ndist;
                            //na = next.cos_azimut(prev, this.path[0]);
                            nextpoint = true;
                            if (!GetNextPoint()) break;
                        }
                    #endregion
                    #region NextPoint
                    if (nextpoint) {
                        if (onPreMove != null) try { onPreMove(next); } catch (Exception e) {
                                Err("error in onPreMove: " + e);
                                if (this.automovestatus > 1) {
                                    Dbg("set automovestatus = 4 - error in onPreMove");
                                    this.automovestatus = 4;
                                }
                            }
                        //if (_core.me.sitOnMount) mr = _core.me.sitMountObj.modelRadius + 1;
                        //                    if (_core.me.sitOnMount) mr = 1.7;
                        //else mr = _core.me.modelRadius + 1;
                        //lastdist = 0;
                        this.ar_target = next;
                        lastdist = 0; stuck = -10;
                        if (prev != null && next != null && next2 != null) {
                            double x3 = prev.X;
                            double x2 = next.X;
                            double x1 = next2.X;
                            double y3 = prev.Y;
                            double y2 = next.Y;
                            double y1 = next2.Y;
                            na = Math.Atan(((x2 - x1) * (y3 - y1) - (x3 - x1) * (y2 - y1)) / ((x2 - x1) * (x3 - x1) + (y2 - y1) * (y3 - y1)));
                            na = Math.Abs(RadianToDegree(na));
                            //Dbg("na = " + na);                            
                        } else na = 0;
                        Dbg("GPS " + this.path.Count + ": " + next.ToString() + " dist=" + _core.me.dist(next.X, next.Y, next.Z) + " na=" + na + " r=" + next.radius + " r2=" + next2.radius);

                        if (this.automovestatus == 6) {
                            this.automovestatus = 5; continue;
                        }
                    }
                    #endregion

                    //////////////////////////////////////////////
                    // states
                    if (this.automovestatus == 2) {
                        // paused state
                        Thread.Sleep(10);
                        if (this.path.Count == 0) {
                            Thread.Sleep(200);
                            continue;
                        }

                        if (!CheckTickets() ||
                            (_core.me.isAfk && this.unpauseonafk)) {
                            ptickets.Clear();
                            //Dbg("set automovestatus = 4 - unpause");
                            this.automovestatus = 4; // не спать, не спать, косить, косить                        
                        }
                        continue;
                    } else if (this.automovestatus == 3) {
                        // go pause state
                        this.autorotatestatus = 3;
                        lastdist = 0;
                        stuck = -10;
                        this.automovestatus = 2;
                        this.autobuffstatus = 2; // pause buffs too
                        StopMoving(); StopRotate();
                        Thread.Sleep(10);
                        continue;
                    } else if (this.automovestatus == 4) {
                        // unpause state
                        Thread.Sleep(10);
                        if (!GetNextPoint()) {
                            Err("Can't move - path is empty");
                            this.automovestatus = 2;
                            Thread.Sleep(150);
                            continue;
                        }
                        if (onPreMove != null) try { onPreMove(next); } catch (Exception e) { Err("error in onPreMove: " + e); }
                        CallDoMount();
                        //                    if (onslave) {
                        //                        var slv = _core.getSlave();
                        //                        if (slv != null) mr = slv.modelRadius;
                        //                    }
                        if (!onslave) _core.ComeTo(this.next.X, this.next.Y, this.next.Z, 1, this.next.distZ(_core.me));
                        if (me.distZ(next) > this.lazydist) { // crazy solution
                            var newpath = this.GeneratePath(this.path[0], me);
                            if (newpath.Count == 0) {
                                Dbg("left " + this.path.Count);
                                Err("Патирялася я " + me.distZ(next) + " " + next);
                                lasterror = GLONASSLastError.TargetLost;
                                Stop();
                            }
                            newpath.AddRange(this.path);
                            this.path = newpath;
                        }


                        if (PilotMode) { this.automovestatus = 5; this.autobuffstatus = 1; continue; }
                        lastdist = 0; stuck = -10; this.autorotatestatus = 4; this.automovestatus = 1; this.autobuffstatus = 1;
                        Thread.Sleep(100);
                        if (!_core.moveForwardState) _core.MoveForward(true);
                        this.ar_target = next;
                    } else if (this.automovestatus == 1) {
                        if (!_core.moveForwardState) _core.MoveForward(true); // show must go on
                        this.autorotatestatus = 4;
                        this.ar_target = next; // todo
                    }
                    //SetMapPos(next.x,next.y,next.z);

                    // drift
                    // притормаживаем на резких поворотах
                    if (next != null && next2 != null && na > 30 && _core.moveForwardState
                        && pathspeed > 0.4 && me.distZ(next) < pathspeed * 4) {
                        //int stop = (int)Math.Round(na * (2 * pathspeed));
                        int stop = (int)Math.Round(na * (pathspeed));
                        Dbg("@drift@ na=" + na + " pathspeed=" + pathspeed + " sleep=" + stop);
                        //na = 0; // once
                        _core.MoveForward(false);
                        Dbg("ABS on");
                        Thread.Sleep(stop);
                        //Dbg("end stop");
                    }

                    if (next != null) {
                        SetMe();
                        this.pathspeed = lastdist - me.distNoZ(next);

                        if (this.pathspeed <= 0.001 && this.automovestatus == 1 && Math.Abs(an) < 75) {
                            stuck++;
                            //Dbg("stuck=" + stuck + " pathspeed=" + this.pathspeed + " lastdist=" + lastdist);
                            if (this.onslave && _core.moveForwardState) {
                                if (stuck > 3) {
                                    StopRotate();
                                    try { _core.MoveForward(false); Thread.Sleep(1351); } finally { if (!_core.moveForwardState) _core.MoveForward(true); }
                                    lastdist = 0;
                                    stuck = 0;
                                }
                            } else if (stuck == 5) {    // dveri
                                try {
                                    var dveri = _core.getDoodads().Where(d => d.dbAlmighty.groupId == 36 && d.dbFuncGroup.useSkills.Exists(s => s.id == 13439) && _core.me.dist(d) < 3).OrderBy(d => _core.me.dist(d)).FirstOrDefault(); // 2m
                                    //Dbg("dd=" + dveri.dbAlmighty.groupId + " sk="+dveri.getUseSkills().First().id); //  d.dbAlmighty.groupId == 68 &&
                                    if (dveri != null) {
                                        Dbg("door");
                                        _core.MoveForward(false);
                                        _core.UseDoodadSkill(13439, dveri, 0, false, 5);
                                        //_core.UseDoodadSkill(16828, dveri, 0, false, 5);
                                        waitCoolDown();
                                        Thread.Sleep(150);
                                        lastdist = 0;
                                    } // else Dbg("door not found");
                                } catch { }
                            } else if (stuck == 6) {
                                // 
                                try { _core.Jump(true); Thread.Sleep(70); } finally { _core.Jump(false); }
                                Dbg("jump");
                                Thread.Sleep(160);

                                //this.pathspeed = lastdist - me.distNoZ(next);
                                //if (this.pathspeed > 0) stuck = 0;
                            } else if (stuck > 12) {
                                _core.MoveTo(this.next.X, this.next.Y, this.next.Z);
                            }
                        } else if (stuck > 0) stuck = 0;
                    }
                    if (next != null) lastdist = me.distNoZ(next);//_core.me.dist(next.x, next.y, next.z);
                    //Dbg("speed " + this.speed);
                } catch (ThreadAbortException) { break; } catch (Exception en) { Err("Primary reactor faiure " + en); Thread.Sleep(500); }

            }
            Dbg("Reactor shotdown");
            this.autorotatestatus = -1;
            this.autobuffstatus = -1;
            Stop();
            return;
        }
        private double RadianToDegree(double angle) {
            return angle * (180.0 / Math.PI);
        }
        private double DegreeToRadian(double angle) {
            return Math.PI * angle / 180.0;
        }
        private void CallDoMount() {
            if (onMount != null) try { onMount(this.totaldist); } catch (Exception e) { Err("error in onMount: " + e); }
        }

        private void SetMe() {
            this.onslave = _core.me.sitOnMount && _core.getMount() != _core.me.sitMountObj;
            if (_core.me.sitOnMount) me = _core.me.sitMountObj;
            else me = _core.me;
        }

        private bool GetNextPoint() {
            if (this.path.Count == 0) {
                this.automovestatus = 2;
                return false;
            }
            SetMe();
            if (next != null && prev.distZ(next) > 1 + me.radius) prev = next;
            else prev = me;
            next = this.path[0];
            if (this.path.Count > 1) next2 = this.path[1];
            return true;
        }

        private void StopMoving() {
            if (_core.moveForwardState) _core.MoveForward(false);
            if (_core.moveBackwardState) _core.MoveBackward(false);
            if (_core.jumpState) _core.Jump(false);
        }
        #endregion
        #region MovingBuffs
        private void MovingBuffs()   //Баффы в движении. Точим морковь и поем песенки
        {
            var timers = new Dictionary<string, DateTime>() { { "Рывок", DateTime.Now }, { "Походный марш", DateTime.Now } };
            timers["Песнь исцеления"] = DateTime.Now;
            timers["Отставить бунт!"] = DateTime.Now;

            while (this.autobuffstatus > 0) {
                try {
                    Thread.Sleep(this.random.Next(210, 430));

                    if (this.autobuffstatus == 2) continue;
                    if (_core.isInPeaceZone() || !_core.isAlive()) continue;
                    waitCoolDown();
                    if (_core.me.isAfk) _core.UpdateNoAfkState();
                    if (onBuff != null) try { onBuff(this.totaldist); } catch (Exception e) { Err("error in onBuff: " + e); }

                    if (this.onslave &&
                        (_core.buffTime(_core.getBuff(_core.getSlave(), "Отставить бунт!")) == 0 || timers["Отставить бунт!"] < DateTime.Now)) {
                        //                    Dbg("cast Отставить бунт!");
                        timers["Отставить бунт!"] = DateTime.Now.AddSeconds(this.random.Next(31, 50));
                        if (!_core.UseSkill("Отставить бунт!", true)) Err(" err=" + _core.GetLastError());
                    }
                    if (this.onslave) continue; // no more buffs for slave

                    if (!_core.GetGroupStatus("collector") && _core.me.sitOnMount && this.totaldist < (lazydist / 4)
                            && IHaveSkill("Бесхитростная мечта") && _core.getMount().level >= 30
                            && _core.buffTime(_core.getBuff(_core.getMount(), "Бесхитростная мечта")) == 0 && _core.me.isMoving
                            && hasPack() && _core.itemCount("Морковь") > 10) {
                        _core.UseSkill("Бесхитростная мечта", true);
                        continue;
                    }
                    if (_core.me.isMoving && _core.me.sitOnMount && IHaveSkill("Рывок")
                            && !hasPack() && timers["Рывок"] < DateTime.Now) {
                        timers["Рывок"] = DateTime.Now.AddSeconds(this.random.Next(320, 330) / 10);
                        _core.UseSkill("Рывок", true);
                        waitCoolDown();
                    }
                    if (_core.me.isMoving && (_core.me.sitOnMount || hasPack()) && IHaveSkill("Походный марш")
                            && (_core.me.getBuffs().Where(x => x.name.Contains("Походный марш")).Count() < 1 || timers["Походный марш"] < DateTime.Now)) {
                        timers["Походный марш"] = DateTime.Now.AddSeconds(this.random.Next(120, 145) / 10);
                        _core.UseSkill("Походный марш", true);
                        continue;
                    }
                    if ((_core.me.hpp < 90 || (_core.me.sitOnMount && _core.getMount().hpp < 90)) && IHaveSkill("Песнь исцеления")
                        && (_core.me.getBuffs().Where(x => x.name.Contains("Песнь исцеления")).Count() < 1 || timers["Песнь исцеления"] < DateTime.Now)) {
                        timers["Песнь исцеления"] = DateTime.Now.AddSeconds(this.random.Next(120, 145) / 10);
                        _core.UseSkill("Песнь исцеления", true);
                        continue;
                    }
                } catch (ThreadAbortException) { break; } catch (Exception ae) { Err("Life support system failure " + ae); Thread.Sleep(500); }
            }
        }
        /// <summary>
        /// check if have pack
        /// </summary>
        /// <returns>true if have</returns>
        public bool hasPack() {
            return _core.buffTime("Переноска груза") != 0;
        }
        /// <summary>
        /// wait for GKD + current cast
        /// </summary>
        /// <returns>always true; for use in chains</returns>
        public bool waitCoolDown() {
            while (_core.me.isGlobalCooldown) Thread.Sleep(50);
            while (_core.me.isCasting) Thread.Sleep(50);
            return true;
        }
        /// <summary>
        /// check if have skill
        /// </summary>
        /// <param name="name"></param>
        /// <returns>true if have</returns>
        public bool IHaveSkill(string name) {
            return _core.me.getSkills().Where(x => x.name == name).Count() > 0;
        }
        #endregion
        #region Task control
        private void TaskStart(ref Task task, Action func) {
            TaskStop(ref task);
            task = new Task(func);
            task.Start();
            return;
        }
        private void TaskStop(ref Task task) {
            if (task != null) {
                //if (task.IsCompleted == false || task.Status == TaskStatus.Running ||
                //           task.Status == TaskStatus.WaitingToRun ||  task.Status == TaskStatus.WaitingForActivation) this.autobuff.??;
                try {
                    if (task.Status == TaskStatus.RanToCompletion
                        || task.Status == TaskStatus.Running
                        || task.Status == TaskStatus.WaitingToRun
                        || task.Status == TaskStatus.WaitingForChildrenToComplete
                        ) task.Dispose();
                    // some times it's not helps...
                } catch { ; }
            }
        }
        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose() {
            this.automovestatus = -1;
            this.autorotatestatus = -1;
            this.autobuffstatus = -1;
            Thread.Sleep(50);
            StopMoving();
            StopRotate();
            TaskStop(ref this.automove);
            TaskStop(ref this.autorotate);
            TaskStop(ref this.autobuff);
            ptickets.ForEach(t => t.Dispose());
            ptickets.Clear();
            actions.Clear();
        }
        #endregion
        #region Logging tools
        /// <summary>
        /// Colored Log
        /// </summary>
        /// <param name="color">Color of out</param>
        /// <param name="str">output string</param>
        public void Log(Color color, string str) {

            _core.LogSetColor(color);
            _core.Log(DateTime.Now.ToLongTimeString() + ": " + str);
            _core.LogSetColor(Color.White);
        }
        /// <summary>
        /// Error logging
        /// <code>
        /// if(SomeCheckReturnigFalseOnFail() || Nav.Err("Fail happens")) { DoSome(); }
        /// else return;
        /// </code>
        /// </summary><see cref="Dbg"/>
        /// <param name="str">output string</param>
        /// <returns>always (bool)false</returns>
        public bool Err(string str) { Log(Color.Red, str); return false; }
        /// <summary>
        /// Debug logging
        /// </summary><see cref="Err"/>
        /// <param name="str">output string</param>
        /// <returns>always (bool)false</returns>
        public bool Dbg(string str) { if (this.DEBUG) Log(Color.Gray, str); return false; }
        #endregion
        #region Old and Deprecated
        /// <summary>
        /// get dest
        /// </summary>
        /// <returns>current destination point name</returns>
        public string Dest() {
            if (this.path.Count == 0) return "Ась?";
            return this.path.LastOrDefault().name;
        }
        #endregion

    }

}
