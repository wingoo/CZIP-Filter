using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace CZIP_Filter
{
    class Program
    {
        static void Main(string[] args)
        {
            //1.过滤省市地址
            var ipList = FilterIP();

            //2.合并ip
            var count = 0;
            var deadCount = 0;
            var currList = MergeIP(ipList, ref count);
            while (count != 0 && deadCount != 400)
            {
                Console.WriteLine(deadCount + ",当前ip数量:" + currList.Count + ",合并个数" + count);

                currList = MergeIP(currList, ref count);
                deadCount++;
            }

            Console.WriteLine("当前ip数量" + currList.Count);

            //3.拼接
            var sb = new StringBuilder();
            foreach (var ip in currList)
            {
                sb.AppendLine(string.Format("{0},{1},{2},{3}", ip.StartIP, ip.EndIP, ip.Province, ip.City));
            }
            File.WriteAllText("ips2.txt", sb.ToString(), Encoding.UTF8);

            Console.ReadKey();
        }

        /// <summary>
        /// 合并ip地址,根据当前的endip查找是否有startip相同的,有则合并
        /// </summary>
        /// <param name="allList">所有ip地址</param>
        /// <param name="count">合并的个数</param>
        /// <returns>合并后list</returns>
        private static List<IPEntity> MergeIP(List<IPEntity> allList, ref int count)
        {
            //1.获取所有的startip
            var dicStart = new Dictionary<long, IPEntity>();
            foreach (var ip in allList)
            {
                dicStart.Add(ip.StartIP, ip);
            }

            var removeList = new Dictionary<long, object>();
            var currList = new List<IPEntity>();

            for (var i = 0; i < allList.Count; i++)
            {
                var ip = allList[i];
                if (removeList.ContainsKey(ip.StartIP))
                {
                    continue;
                }

                //判断当前的endip+1是否为某个的startip
                var nextStartIP = ip.EndIP + 1;
                if (dicStart.ContainsKey(nextStartIP) && dicStart[nextStartIP].City == ip.City)
                {
                    currList.Add(new IPEntity { StartIP = ip.StartIP, EndIP = dicStart[nextStartIP].EndIP, City = ip.City, Province = ip.Province });
                    removeList.Add(nextStartIP, null);//如果已经合并,则会过滤掉
                }
                else
                {
                    currList.Add(ip);
                }
            }

            count = removeList.Count;

            return currList;
        }

        /// <summary>
        /// 过滤ip地址
        /// </summary>
        /// <returns>符合要求的ip列表</returns>
        private static List<IPEntity> FilterIP()
        {
            var allLine = File.ReadAllLines(FileName, Encoding.UTF8);
            var allCount = 0;
            var currCount = 0;
            var ipList = new List<IPEntity>();

            foreach (var line in allLine)
            {
                var cols = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (cols.Length == 4)
                {
                    //只记录长度为4的,其他多为国外的地址
                    //58.48.76.40     58.48.83.36     湖北省武汉市 电信
                    //只定位到一级城市,此城市下面的县市区不定位
                    var isValid = false; //是否有效

                    //0.将大学替换为所在城市
                    //59.66.156.63    59.66.156.65    清华大学紫荆公寓6号楼 121B
                    if (cols[2].Contains("大学"))
                    {
                        foreach (var pair in uDic)
                        {
                            if (cols[2].Contains(pair.Key))
                            {
                                cols[2] = pair.Value;
                                break;
                            }
                        }
                    }

                    //1.检查是否包含省的名字,(国外地址会过滤掉)
                    string province = null;
                    for (var i = 0; i < proList.Count; i++)
                    {
                        if (cols[2].Contains(proList[i]))
                        {
                            isValid = true;
                            province = proList[i];
                            break;
                        }
                    }

                    if (!isValid)
                    {
                        continue;
                    }

                    //2.城市替换
                    string city = null;
                    if (directCityList.Contains(province))
                    {
                        //直辖市,包含港澳
                        city = province;
                    }
                    else
                    {
                        //非直辖市
                        if (provinceCity.ContainsKey(province))
                        {
                            foreach (var c in provinceCity[province])
                            {
                                if (cols[2].Contains(c))//如果里面含有城市名,则为这个城市
                                {
                                    city = c;
                                    break;
                                }
                            }

                            //如果没找到,则默认为省会
                            if (city == null)
                            {
                                city = cityList[proList.IndexOf(province)];
                            }
                        }
                        else
                        {
                            Console.WriteLine(province);
                        }
                    }

                    if (city != null && province != null)
                    {
                        ipList.Add(new IPEntity { StartIP = IpToInt(cols[0]), EndIP = IpToInt(cols[1]), Province = province, City = city });
                    }

                    currCount++;
                }

                allCount++;
            }

            Console.WriteLine("所有数量:" + allCount);
            Console.WriteLine("有效数量:" + currCount);

            return ipList;
        }

        /// <summary>
        /// convert ip to integer
        /// http://stackoverflow.com/a/461770/205592
        /// </summary>
        /// <param name="ip">source ip string, like 127.0.0.1</param>
        /// <returns>the ip integer</returns>
        private static long IpToInt(string ip)
        {
            var address = IPAddress.Parse(ip);
            return (long)(uint)IPAddress.NetworkToHostOrder(BitConverter.ToInt32(address.GetAddressBytes(), 0));
        }

        /// <summary>
        /// ipentity
        /// </summary>
        public class IPEntity
        {
            public long StartIP;
            public long EndIP;
            public string Province;
            public string City;
        }

        private const string FileName = "ips.txt";
        private static readonly List<string> directCityList = new List<string> { "北京", "天津", "上海", "重庆", "香港", "澳门" };//直辖市
        private static readonly List<string> proList = new List<string> { "北京", "天津", "上海", "重庆", 
                "河北", "山西", "辽宁", "吉林", "黑龙江", "江苏", "浙江", "安徽", "福建", "江西", "山东", "河南", "湖北", "湖南", "广东", "海南", "四川", "贵州", "云南", "陕西", 
                "甘肃", "青海", "台湾", "内蒙古", "广西", "西藏", "宁夏", "新疆", "香港", "澳门" };//省,直辖市
        private static readonly List<string> cityList = new List<string> { "北京", "天津", "上海", "重庆", 
                "石家庄", "太原", "沈阳", "长春", "哈尔滨", "南京", "杭州", "合肥", "福州", "南昌", "济南", "郑州", "武汉", "长沙", "广州", "海口", "成都", "贵阳", "昆明", "西安", 
                "兰州", "西宁", "台北市", "呼和浩特", "南宁", "拉萨", "银川", "乌鲁木齐", "香港", "澳门" };//省会,对应proList
        private static readonly Dictionary<string, string> uDic = new Dictionary<string, string> { { "江西财经大学", "江西省南昌市" }, { "长江大学", "湖北省荆州市" }, { "黑龙江大学", "黑龙江省哈尔滨市" }, { "北京交通大学", "北京市" }, { "中央财经大学", "北京市" }, { "北京邮电大学", "北京市" }, { "对外经济贸易大学", "北京市" }, { "清华大学", "北京市" }, { "华中农业大学", "湖北省武汉市" }, { "吉林大学", "吉林省长春市" }, { "厦门大学", "福建省厦门市" }, { "上海交通大学", "上海市" }, { "宁波大学", "福建省宁波市" }, { "浙江工业大学", "浙江省杭州市" }, { "北京市房山区理工大学", "北京市" }, { "北京工业大学", "北京市" }, { "北京科技大学", "北京市" }, { "北京航空航天大学", "北京市" }, { "华东师范大学", "上海市" }, { "合肥工业大学", "安徽省合肥市" }, { "云南大学", "云南省昆明市" }, { "浙江广播电视大学", "浙江省杭州市" }, { "兰州大学", "甘肃省兰州市" }, { "佳木斯大学", "黑龙江省佳木斯市" }, { "湖北大学", "湖北省武汉市" }, { "西安科技大学", "陕西省西安市" }, { "长沙理工大学", "湖南省长沙市" }, { "湖南省长沙理工大学", "湖南省长沙市" }, { "湖南农业大学", "湖南省长沙市" }, { "贵州工业大学", "贵州省贵阳市" }, { "安徽农业大学", "安徽省合肥市" }, { "安徽大学", "安徽省合肥市" }, { "华东交通大学", "江西省南昌市" }, { "同济大学", "上海市" }, { "上海财经大学", "上海市" }, { "华南师范大学", "广东省广州市" }, { "台湾大学", "台湾省台北市" }, { "北京理工大学", "北京市" }, { "北京大学", "北京市" }, { "华东理工大学", "上海市" }, { "北京化工大学", "北京市" }, { "华南理工大学", "广东省广州市" }, { "四川大学", "四川省成都市" }, { "内蒙古农业大学", "内蒙古自治区呼和浩特市" }, { "东华理工大学", "江西省南昌市" }, { "北京师范大学", "北京市" }, { "中国农业大学", "北京市" }, { "南开大学", "天津市" }, { "武汉大学", "湖北省武汉市" }, { "西华大学", "四川省成都市" }, { "中山大学", "广东省广州市" }, { "华南农业大学", "广东省广州市" }, { "西安交通大学", "陕西省西安市" }, { "大连理工大学", "辽宁省大连市" }, { "哈尔滨工程大学", "黑龙江省哈尔滨市" }, { "哈尔滨理工大学", "黑龙江省哈尔滨市" }, { "东北林业大学", "东北林业大学" }, { "东南大学", "江苏省南京市" }, { "南京大学", "江苏省南京市" }, { "上海大学", "上海市" }, { "东华大学", "上海市" }, { "上海师范大学", "上海市" }, { "郑州大学", "河南省郑州市" }, { "湖南师范大学", "湖南省长沙市" }, { "大庆职工大学", "黑龙江省大庆市" }, { "长春工业大学", "吉林省长春市" }, { "西安石油大学", "陕西省西安市" }, { "重庆大学", "重庆市" }, { "北方工业大学", "北京市" }, { "北京中医药大学", "北京市" }, { "北京林业大学", "北京市" }, { "首都经贸大学", "北京市" }, { "首都师范大学", "北京市" }, { "北京联合大学", "北京市" }, { "青海大学", "青海省西宁市" }, { "集美大学", "福建省厦门市" }, { "黑龙江广播电视大学", "黑龙江省哈尔滨市" }, { "北京体育大学", "北京市" }, { "燕山大学", "河北省秦皇岛市" }, { "中国人民大学", "北京市" }, { "福州大学", "福建省福州市" }, { "北京信息科技大学", "北京市" }, { "南京理工大学", "江苏省南京市" }, { "宁夏大学", "宁夏回族自治区银川市" }, { "四川师范大学", "四川省成都市" }, { "湖南科技大学", "湖南省湘潭市" }, { "首都科技大学", "北京市" }, { "上海理工大学", "上海市" }, { "南京工业大学", "江苏省南京市" }, { "四川西南科技大学", "四川省绵阳市" }, { "成都理工大学", "四川省成都市" }, { "哈尔滨师范大学", "黑龙江省哈尔滨市" }, { "太原科技大学", "山西省太原市" }, { "中北大学", "山西省太原市" }, { "黄河科技大学", "河南省郑州市" }, { "青岛大学", "山东省青岛市" }, { "山东大学", "山东省济南市" }, { "东北大学", "辽宁省沈阳市" }, { "江西师范大学", "江西省南昌市" }, { "南昌大学", "江西省南昌市" }, { "四川农业大学", "四川省成都市" }, { "成都中医药大学", "四川省成都市" }, { "南京化工大学", "江苏省南京市" }, { "浙江大学", "浙江省杭州市" }, { "新疆大学", "新疆维吾尔自治区乌鲁木齐市" }, { "中南大学", "湖南省长沙市" }, { "华中科技大学", "湖北省武汉市" }, { "武汉科技大学", "湖北省武汉市" }, { "湖北省武汉科技大学", "湖北省武汉市" }, { "中南财经政法大学", "湖北省武汉市" }, { "西安建筑科技大学", "陕西省西安市" }, { "西北工业大学", "陕西省西安市" }, { "青岛科技大学", "山东省青岛市" }, { "河北大学", "河北省保定市" }, { "东北财经大学", "辽宁省大连市" }, { "东北农业大学", "黑龙江省哈尔滨市" }, { "哈尔滨工业大学", "黑龙江省哈尔滨市" }, { "暨南大学", "广东省广州市" }, { "北京朝阳区(芍药居)经贸大学", "北京市" }, { "西安联合大学", "陕西省西安市" }, { "安徽财经大学", "安徽省蚌埠市" }, { "成都大学", "四川省成都市" } };//大学对应城市
        private static readonly Dictionary<string, List<string>> provinceCity = new Dictionary<string, List<string>> { { "河北", new List<string> { "石家庄", "廊坊", "衡水", "唐山", "秦皇岛", "邯郸", "邢台", "保定", "张家口", "承德", "沧州" } }, { "山西", new List<string> { "太原", "临汾", "吕梁", "大同", "阳泉", "长治", "晋城", "朔州", "晋中", "运城", "忻州" } }, { "内蒙古", new List<string> { "呼和浩特", "包头", "兴安", "锡林郭勒", "阿拉善", "乌海", "赤峰", "通辽", "鄂尔多斯", "呼伦贝尔", "巴彦淖尔", "乌兰察布" } }, { "辽宁", new List<string> { "沈阳", "辽阳", "盘锦", "铁岭", "朝阳", "葫芦岛", "大连", "鞍山", "抚顺", "本溪", "丹东", "锦州", "营口", "阜新" } }, { "吉林", new List<string> { "长春", "吉林", "延边", "四平", "辽源", "通化", "白山", "松原", "白城" } }, { "黑龙江", new List<string> { "哈尔滨", "牡丹江", "黑河", "绥化", "齐齐哈尔", "大兴安岭", "鸡西", "鹤岗", "双鸭山", "大庆", "伊春", "佳木斯", "七台河" } }, { "江苏", new List<string> { "南京", "扬州", "镇江", "泰州", "宿迁", "无锡", "徐州", "常州", "苏州", "南通", "连云港", "淮安", "盐城" } }, { "浙江", new List<string> { "杭州", "台州", "丽水", "宁波", "温州", "嘉兴", "湖州", "绍兴", "金华", "衢州", "舟山" } }, { "安徽", new List<string> { "合肥", "黄山", "滁州", "阜阳", "宿州", "巢湖", "六安", "亳州", "池州", "宣城", "芜湖", "蚌埠", "淮南", "马鞍山", "淮北", "铜陵", "安庆" } }, { "福建", new List<string> { "福州", "厦门", "莆田", "三明", "泉州", "漳州", "南平", "龙岩", "宁德" } }, { "江西", new List<string> { "南昌", "抚州", "上饶", "景德镇", "萍乡", "九江", "新余", "鹰潭", "赣州", "吉安", "宜春" } }, { "山东", new List<string> { "济南", "威海", "日照", "莱芜", "临沂", "德州", "聊城", "滨州", "菏泽", "青岛", "淄博", "枣庄", "东营", "烟台", "潍坊", "济宁", "泰安" } }, { "河南", new List<string> { "郑州", "许昌", "漯河", "三门峡", "南阳", "商丘", "信阳", "周口", "驻马店", "济源", "开封", "洛阳", "平顶山", "安阳", "鹤壁", "新乡", "焦作", "濮阳" } }, { "湖北", new List<string> { "武汉", "荆州", "黄冈", "咸宁", "随州", "黄石", "恩施", "十堰", "宜昌", "襄樊", "鄂州", "荆门", "孝感", "仙桃", "潜江", "天门", "神农架" } }, { "湖南", new List<string> { "长沙", "郴州", "永州", "怀化", "娄底", "株洲", "湘潭", "湘西", "衡阳", "邵阳", "岳阳", "常德", "张家界", "益阳" } }, { "广东", new List<string> { "广州", "肇庆", "惠州", "梅州", "汕尾", "河源", "阳江", "清远", "东莞", "韶关", "中山", "深圳", "珠海", "汕头", "潮州", "揭阳", "云浮", "佛山", "江门", "湛江", "茂名" } }, { "广西", new List<string> { "南宁", "百色", "贺州", "河池", "来宾", "崇左", "柳州", "桂林", "梧州", "北海", "防城港", "钦州", "贵港", "玉林" } }, { "海南", new List<string> { "海口", "三亚", "五指山", "琼海", "儋州", "文昌", "万宁", "东方", "定安", "屯昌", "澄迈", "临高", "白沙", "昌江", "乐东", "陵水", "保亭", "琼中", "西沙", "南沙", "中沙" } }, { "四川", new List<string> { "成都", "内江", "乐山", "南充", "眉山", "宜宾", "广安", "达州", "雅安", "巴中", "资阳", "自贡", "阿坝", "甘孜", "凉山", "攀枝花", "泸州", "德阳", "绵阳", "广元", "遂宁" } }, { "贵州", new List<string> { "贵阳", "六盘水", "铜仁", "黔西南", "毕节", "黔东南", "黔南", "遵义", "安顺" } }, { "云南", new List<string> { "昆明", "楚雄", "红河", "文山", "西双版纳", "大理", "曲靖", "德宏", "怒江", "迪庆", "玉溪", "保山", "昭通", "丽江", "普洱", "临沧" } }, { "西藏", new List<string> { "拉萨", "昌都", "山南", "日喀则", "那曲", "阿里", "林芝" } }, { "陕西", new List<string> { "西安", "商洛", "铜川", "宝鸡", "咸阳", "渭南", "延安", "汉中", "榆林", "安康" } }, { "甘肃", new List<string> { "兰州市", "庆阳", "定西", "陇南", "嘉峪关", "临夏", "金昌", "甘南", "白银", "天水", "武威", "张掖", "平凉", "酒泉" } }, { "青海", new List<string> { "西宁", "海东", "海北", "黄南", "海南", "果洛", "玉树", "海西" } }, { "宁夏", new List<string> { "银川", "石嘴山", "吴忠", "固原", "中卫" } }, { "新疆", new List<string> { "乌鲁木齐", "克拉玛依", "吐鲁番", "哈密", "昌吉", "博尔塔拉", "巴音郭楞", "阿克苏", "克孜勒苏", "喀什", "和田", "伊犁", "塔城", "阿勒泰", "石河子", "阿拉尔", "图木舒克", "五家渠" } }, { "台湾", new List<string> { "台北市", "桃园县", "新竹县", "苗栗县", "台中县", "彰化县", "南投县", "云林县", "嘉义县", "台南县", "高雄县", "高雄市", "屏东县", "澎湖县", "台东县", "花莲县", "基隆市", "台中市", "台南市", "新竹市", "嘉义市", "台北县", "宜兰县" } }}; 
    
    }
}
