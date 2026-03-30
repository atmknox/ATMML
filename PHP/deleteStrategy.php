<?php 

$name = $_GET["n"];

/* Database config */
$db_host		= 'localhost';
$db_user		= 'atmstudies';
$db_pass		= 'zbz82FySFNWvv75y';
$db_database	= 'atmstudies'; 
/* End config */

$link = mysql_connect($db_host,$db_user,$db_pass) or die('Unable to establish a data base');

mysql_select_db($db_database);

$sql  = "delete from strategies where Name ='$name'";
$res = mysql_query($sql, $link);

if (!$res) echo mysql_error();
else echo "ok";

?>
