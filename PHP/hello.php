<?php 


$user = $_GET["u"];
$password = $_GET["p"];
$id = $_GET["i"];

/* Database config */
$db_host		= 'localhost';
$db_user		= 'atmstudies';
$db_pass		= 'zbz82FySFNWvv75y';
$db_database	= 'atmstudies'; 
/* End config */

$link = mysql_connect($db_host,$db_user,$db_pass) or die('Unable to establish a data base');

mysql_select_db($db_database);
$sql = "select * from cmrapplogin where User = '$user'";
$res = mysql_query($sql, $link);

if (!$res) echo mysql_error();
$row = mysql_fetch_assoc($res);
$n = mysql_num_rows($res);
echo (($n == 1 /*&& $row["Password"] == $password*/)? "ok" : "bad");
echo "," . $row["EnableMaps"];
echo "," . $row["EnableAlerts"];
echo "," . $row["EnablePositions"];
echo "," . $row["EnableNetworks"];
echo "," . $row["EnableResults"];
echo "," . $row["EnableDiary"];
echo "," . $row["EnablePortfolio"];
echo "," . $row["EnableUS100"];
echo "," . $row["EnableUS500"];
echo "," . $row["EnableMajorETF"];
echo "," . $row["EnableRickStocks"];
echo "," . $row["EnableFinancial"];
echo "," . $row["EnableTradesOnLargeIntervals"];
echo "," . $row["EnableRecommendations"];
echo "," . $row["EnableHistoricalRecommendations"];
echo "," . $row["EnableChartPositions"];
echo "," . $row["EnableCursorColor"];
echo "," . $row["EnableEditTrades"];
echo "," . $row["EnableTradeConditions"];
echo "," . $row["EnableHappenedWithinConditions"];
echo "," . $row["EnableSpreadScans"];
echo "," . $row["EnableCMR"];
echo "," . $row["EnableCurrentRecommendations"];
echo "," . $row["EnableMPMPortfolio"];
echo "," . $row["EnableGlobalIndices"];
echo "," . $row["EnableScore"];
echo "," . $row["EnablePR"];
echo "," . $row["EnableRP"];
echo "," . $row["EnableTRT"];
echo "," . $row["EnableLinks"];
echo "," . $row["EnableADX"];
echo "," . $row["EnablePINZPortfolio"];
echo "," . $row["EnablePINZEditTrades"];
echo "," . $row["EnableSQPTPortfolio"];
echo "," . $row["EnableSQPTEditTrades"];
echo "," . $row["EnableAutoTrade"];
echo "," . $row["EnableServerOutput"];
echo "," . $row["EnableStrategies"];
echo "," . $row["EnableServer"];
echo "," . $row["EnablePINZOrders"];
echo "," . $row["EnablePINZExport"];
echo "," . $row["EnablePRTUExport"];
echo "," . $row["EnablePRTUHistory"];
echo "," . $row["EnableServerTicker"];
echo "," . $row["EnableGuiPortfolio"];

if ($n == 1 /* && $row["Password"] == $password*/) {
  $sql = "update cmrapplogin set session = '$id' where User = '$user'";
  $res = mysql_query($sql, $link);
  if (!$res) echo mysql_error();
}

?>
