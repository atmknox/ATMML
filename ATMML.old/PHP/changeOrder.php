<?php 

$prtu = $_GET["p"];
$ticker = $_GET["t"];
$type = $_GET["a"];
$size = $_GET["s"];
$cancelled = $_GET["c"];

/* Database config */
$db_host		= 'localhost';
$db_user		= 'atmstudies';
$db_pass		= 'zbz82FySFNWvv75y';
$db_database	= 'atmstudies'; 
/* End config */

$link = mysql_connect($db_host,$db_user,$db_pass) or die('Unable to establish a data base');

mysql_select_db($db_database);

$sql  = "update orders ";
$sql .= "set Type = '$type', Size = '$size', Cancelled = '$cancelled' ";
$sql .= "where PRTU = '$prtu' and Ticker = '$ticker'";
$res = mysql_query($sql, $link);

if (!$res) echo mysql_error();
else echo "ok";

?>
