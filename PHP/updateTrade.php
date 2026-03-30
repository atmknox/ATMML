<?php 

$network = $_GET["n"];
$symbol = $_GET["s"];
$hasBloombergId = $_GET["h"];
$bloombergId = $_GET["b"];
$id = $_GET["i1"];
$size = $_GET["z"];
$entryDate = $_GET["d1"];
$entryPrice = $_GET["p1"];
$exitDate = $_GET["d2"];
$exitPrice = $_GET["p2"];
$investment = $_GET["i2"];
$price = $_GET["p"];
$pnl = $_GET["pnl"];

/* Database config */
$db_host		= 'localhost';
$db_user		= 'atmstudies';
$db_pass		= 'zbz82FySFNWvv75y';
$db_database	= 'atmstudies'; 
/* End config */

$link = mysql_connect($db_host,$db_user,$db_pass) or die('Unable to establish a data base');

mysql_select_db($db_database);

$sql = "update trades set Timestamp = now(), Network = '$network', Symbol = '$symbol', HasBloombergId = '$hasBloombergId', BloombergId = '$bloombergId', ";
$sql .= "Size = '$size', EntryDate = '$entryDate', EntryPrice = '$entryPrice', ExitDate = '$exitDate', ExitPrice = '$exitPrice', Investment = '$investment', Price = '$price', PnL = '$pnl' where Id = '$id'";
$res = mysql_query($sql, $link);

if (!$res) echo mysql_error();
else echo "ok";

?>
